using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using System.Xml;
using System.IO;
using Microsoft.Win32;
using CodeMarket.IDE.Models;
using CodeMarket.IDE.Services;
using CodeMarket.IDE.ViewModels;

namespace CodeMarket.IDE.Views;
public partial class MainWindow : Window
{
    private readonly BridgeClient bridge = new();
    private readonly SdkIndex sdk = new();
    private readonly LocalReferenceIndex localReferences = new();
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer referenceTimer = new() { Interval=TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer classMapTimer = new() { Interval=TimeSpan.FromMilliseconds(350) };
    private string classMapText="";
    private string? currentPath;
    private string? queuedPath;
    private string? newFolderTarget;
    private string? localPath;
    private string baseline="";
    private bool loading, busy, conflict, completionRequested, forceCompletion, fileOperationPending;
    private bool localDocument, selectingLanguage;
    private string language="csharp";
    private DateTime lastEdit=DateTime.MinValue, completionAt=DateTime.MaxValue, pollAt=DateTime.MinValue;
    private string completionText="";
    private int completionCaret, completionFrom;
    private int completionGeneration;
    private bool referenceBusy;
    private int referenceGeneration;
    private string referenceSignature="";
    private ReferenceHoverRenderer? referenceHover;
    public MainWindow()
    {
        InitializeComponent();
        referenceHover=new ReferenceHoverRenderer(Editor.TextArea.TextView);
        Editor.TextArea.TextView.BackgroundRenderers.Add(referenceHover);
        Editor.MouseMove+=(_,e)=>UpdateReferenceHover(e.GetPosition(Editor));
        Editor.MouseLeave+=(_,_)=>ClearReferenceHover();
        PreviewKeyDown+=(_,e)=> { if(e.Key is Key.LeftCtrl or Key.RightCtrl && Editor.IsMouseOver)Dispatcher.BeginInvoke(()=>UpdateReferenceHover(Mouse.GetPosition(Editor))); };
        PreviewKeyUp+=(_,e)=> { if(e.Key is Key.LeftCtrl or Key.RightCtrl)Dispatcher.BeginInvoke(()=>UpdateReferenceHover(Mouse.GetPosition(Editor))); };
        Deactivated+=(_,_)=>ClearReferenceHover();
        SelectLanguage("csharp");
        Editor.TextArea.Caret.PositionChanged+=EditorSelectionChanged;timer.Tick+=Tick;timer.Start();
        Editor.TextArea.TextEntering+=EditorTextEntering;
        referenceTimer.Tick+=async (_,_)=>await RefreshReferenceAsync();referenceTimer.Start();
        classMapTimer.Tick+=(_,_)=>RefreshClassMap();
        ClassMap.NavigateRequested+=NavigateClassMap;
        RefreshClassMap();
        Reference.PageRequested+=async id=>await OpenReferenceAsync(id);
        Loaded+=async (_,_)=>await ConnectAsync();Closing+=OnClosing;
    }
    private bool Dirty => (localDocument || currentPath is not null) && Editor.Text != baseline;
    private void ShowExplorerClick(object sender,RoutedEventArgs e) { ExplorerPane.Visibility=Visibility.Visible;ClassMap.Visibility=Visibility.Collapsed;ExplorerViewButton.Foreground=System.Windows.Media.Brushes.White;ClassMapViewButton.Foreground=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#858585")!; }
    private void ShowClassMapClick(object sender,RoutedEventArgs e) { ExplorerPane.Visibility=Visibility.Collapsed;ClassMap.Visibility=Visibility.Visible;ExplorerViewButton.Foreground=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#858585")!;ClassMapViewButton.Foreground=System.Windows.Media.Brushes.White;RefreshClassMap(); }
    private void RefreshClassMap() { classMapTimer.Stop();classMapText=Editor.Text;ClassMap.Update(ClassMapParser.Parse(classMapText),localDocument || currentPath is not null); }
    private void NavigateClassMap(int offset)
    {
        if(Editor.Text!=classMapText) { RefreshClassMap();SetStatus("Class map updated; select the symbol again.");return; }
        Editor.CaretOffset=Math.Clamp(offset,0,Editor.Document.TextLength);Editor.Select(Editor.CaretOffset,0);Editor.ScrollToLine(Editor.Document.GetLineByOffset(Editor.CaretOffset).LineNumber);Editor.Focus();
    }
    private void SetStatus(string message) => StatusText.Text=message;
    private async Task RefreshReferenceAsync()
    {
        if(!bridge.Connected || referenceBusy)return;
        referenceBusy=true;int generation=referenceGeneration;
        try
        {
            var response=await bridge.ReferenceIndexAsync();
            if(generation!=referenceGeneration || response.Session!=bridge.Session || response.World!=bridge.World)return;
            string signature=string.Join("\n",response.ReferenceItems ?? []);
            if(signature==referenceSignature)return;
            var entries=(response.ReferenceItems ?? []).Select(raw=>JsonSerializer.Deserialize<ReferenceItem>(raw)).Where(x=>x is not null).Cast<ReferenceItem>().ToList();
            referenceSignature=signature;Reference.UpdateIndex(entries);ClearReferenceHover();
        }
        catch(Exception ex) { if(generation==referenceGeneration)Reference.ShowMessage("Reference unavailable. Restart the game with the updated bridge.\n"+ex.Message); }
        finally { referenceBusy=false; }
    }
    private async Task OpenReferenceAsync(string id)
    {
        if(!bridge.Connected) { SetStatus("Connect to a game world to use the reference.");return; }
        var entry=Reference.Entries.FirstOrDefault(x=>x.Id==id);
        if(entry is null) { SetStatus("That reference entry is not unlocked in this world.");return; }
        int generation=referenceGeneration;
        try
        {
            var response=await bridge.ReferencePageAsync(id);
            if(generation!=referenceGeneration || response.Session!=bridge.Session || response.World!=bridge.World)return;
            Reference.ShowPage(entry,response.ReferenceSubtitle,response.ReferenceText ?? "",response.Related ?? []);
            referenceSignature="";
        }
        catch(Exception ex) { SetStatus(ex.Message); }
    }
    private void SelectLanguage(string key)
    {
        if(key is not ("csharp" or "java"))key="csharp";
        language=key;
        string file=key switch { "java"=>"CodeMarketJava.xshd", _=>"CodeMarketDark.xshd" };
        using var stream=Application.GetResourceStream(new Uri("/CodeMarket.IDE;component/Resources/"+file,UriKind.Relative))!.Stream;
        using var reader=XmlReader.Create(stream);
        Editor.SyntaxHighlighting=HighlightingLoader.Load(reader,HighlightingManager.Instance);
        selectingLanguage=true;
        foreach(ComboBoxItem item in LanguageSelector.Items) if((string)item.Tag==key) { LanguageSelector.SelectedItem=item;break; }
        selectingLanguage=false;
        UpdateReadOnly();
    }
    private void UpdateReadOnly() => Editor.IsReadOnly=fileOperationPending || (!localDocument && currentPath is not null && language!=bridge.Language);
    private void LanguageChanged(object sender,SelectionChangedEventArgs e)
    {
        if(selectingLanguage || LanguageSelector.SelectedItem is not ComboBoxItem item)return;
        ClearReferenceHover();
        completionGeneration++;completionRequested=false;HideCompletion();SelectLanguage((string)item.Tag);
        if(localDocument)SetStatus("Local document — not synced to the game.");
        else if(currentPath is not null && language!=bridge.Language)SetStatus("Preview only: this world uses "+bridge.Language+". Use New Local to edit another language.");
        else if(currentPath is not null)SetStatus("Live sync enabled for the world's language.");
    }
    private bool CanLeaveDocument()
    {
        if(busy || fileOperationPending) { SetStatus("Wait for the current operation to finish.");return false; }
        return !Dirty || MessageBox.Show(this,"Discard unsaved or unsynced changes?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes;
    }
    private void OpenLocalDocument(string text,string? path)
    {
        localDocument=true;localPath=path;currentPath=null;queuedPath=null;conflict=false;baseline=text;
        loading=true;Editor.Text=text;Editor.IsEnabled=true;Editor.CaretOffset=0;loading=false;UpdateReadOnly();
        RefreshClassMap();
        TabTitle.Text=path is null ? "Untitled (local)" : Path.GetFileName(path);
        Breadcrumb.Text=path ?? "Local draft · not synced to the game";Welcome.Visibility=Visibility.Collapsed;
        completionGeneration++;completionRequested=false;HideCompletion();
        SetStatus("Local document — not synced to the game.");
    }
    private void NewLocalClick(object sender,RoutedEventArgs e)
    {
        if(CanLeaveDocument())OpenLocalDocument("",null);
    }
    private void OpenLocalClick(object sender,RoutedEventArgs e)
    {
        if(!CanLeaveDocument())return;
        var dialog=new OpenFileDialog { Filter="Source files|*.cs;*.java|All files|*.*" };
        if(dialog.ShowDialog(this)!=true)return;
        try
        {
            string text=File.ReadAllText(dialog.FileName);
            localDocument=true;
            string? key=Path.GetExtension(dialog.FileName).ToLowerInvariant() switch { ".cs"=>"csharp", ".java"=>"java", _=>null };
            if(key is not null)SelectLanguage(key);
            OpenLocalDocument(text,dialog.FileName);
        }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"Open local file",MessageBoxButton.OK,MessageBoxImage.Error); }
    }
    private void SaveLocalClick(object sender,RoutedEventArgs e)
    {
        if(!localDocument) { SetStatus("Save Local is for local drafts; game scripts sync automatically.");return; }
        if(localPath is null)
        {
            string ext=language switch { "java"=>"java", _=>"cs" };
            var dialog=new SaveFileDialog { Filter="Source files|*."+ext+"|All files|*.*",DefaultExt="."+ext,AddExtension=true };
            if(dialog.ShowDialog(this)!=true)return;
            localPath=dialog.FileName;
        }
        try { File.WriteAllText(localPath,Editor.Text);baseline=Editor.Text;TabTitle.Text=Path.GetFileName(localPath);Breadcrumb.Text=localPath;SetStatus("Saved local file: "+localPath); }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"Save local file",MessageBoxButton.OK,MessageBoxImage.Error); }
    }
    private async void ConnectClick(object sender,RoutedEventArgs e) => await ConnectAsync();
    private async Task ConnectAsync()
    {
        if (busy) { SetStatus("Bridge is busy; try again shortly.");return; }
        if (Dirty && MessageBox.Show(this,"Discard unsynced edits and reconnect?","Unsynced changes",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        ClearReferenceHover();
        busy=true;SetStatus("Connecting to game…");
        try
        {
            var result=await bridge.ConnectAsync(); UpdateExplorer(result);
            currentPath=null;queuedPath=null;localDocument=false;localPath=null;baseline="";conflict=false;sdk.SetLanguage(bridge.Language);SelectLanguage(bridge.Language ?? "csharp");loading=true;Editor.Clear();Editor.IsEnabled=false;loading=false;Welcome.Visibility=Visibility.Visible;
            RefreshClassMap();
            HideCompletion();TabTitle.Text="Welcome";Breadcrumb.Text="Select a script in Explorer";
            ConnectionText.Text=$"World {bridge.World}  ·  {bridge.Language}  ·  Connected";SetStatus("Connected. Select a script to begin.");
            referenceGeneration++;referenceSignature="";Reference.ShowDisconnected();_ = RefreshReferenceAsync();
        }
        catch(Exception ex) { bridge.Disconnect();referenceGeneration++;referenceSignature="";Reference.ShowDisconnected();ConnectionText.Text="Disconnected";SetStatus(ex.Message); }
        finally { busy=false; }
    }
    private async void ExplorerSelected(object sender,RoutedPropertyChangedEventArgs<object> e)
    {
        if(e.NewValue is ExplorerNode)newFolderTarget=null;
        if (e.NewValue is not ExplorerNode { Path: not null } node || node.Path==currentPath)return;
        if (!bridge.Connected) { SetStatus("Connect to the game first.");return; }
        if(localDocument && Dirty && MessageBox.Show(this,"Discard unsaved local changes and open this game script?","Unsaved local changes",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        if (busy || (!localDocument && Dirty)) { queuedPath=node.Path;SetStatus("Syncing edits before opening the selected script…");return; }
        await OpenScriptAsync(node.Path);
    }
    private async Task OpenScriptAsync(string path)
    {
        busy=true;
        try { var result=await bridge.ReadAsync(path);LoadText(path,result.Text ?? ""); }
        catch(Exception ex) { SetStatus(ex.Message); }
        finally { busy=false; }
    }
    private void LoadText(string path,string text)
    {
        currentPath=path;queuedPath=null;localDocument=false;localPath=null;baseline=text;conflict=false;SelectLanguage(bridge.Language ?? "csharp");loading=true;Editor.Text=text;Editor.IsEnabled=true;Editor.CaretOffset=0;loading=false;UpdateReadOnly();
        RefreshClassMap();
        TabTitle.Text=path.Split('/').Last();Breadcrumb.Text=path.Replace("/","  ›  ");Welcome.Visibility=Visibility.Collapsed;HideCompletion();pollAt=DateTime.UtcNow.AddSeconds(1);SetStatus("Live sync enabled.");
    }
    private async void ReloadClick(object sender,RoutedEventArgs e)
    {
        if(localDocument)
        {
            if(localPath is null) { SetStatus("This local draft has not been saved yet.");return; }
            if(Dirty && MessageBox.Show(this,"Discard unsaved changes and reload this local file?","Reload",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            try { OpenLocalDocument(File.ReadAllText(localPath),localPath); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"Reload local file",MessageBoxButton.OK,MessageBoxImage.Error); }
            return;
        }
        if(currentPath is null || busy)return;
        if(Dirty && MessageBox.Show(this,"Discard unsynced edits and reload from the game?","Reload",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        busy=true;try { var result=await bridge.ReadAsync(currentPath);LoadText(currentPath,result.Text ?? ""); } catch(Exception ex){SetStatus(ex.Message);}finally{busy=false;}
    }
    private void UpdateExplorer(BridgeResponse response,string? selectFolder=null)
    {
        newFolderTarget=selectFolder;
        Explorer.ItemsSource=ExplorerNode.FromPaths(response.Files ?? [],response.Folders,selectFolder);
        if(selectFolder is not null) Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>SelectFolderInExplorer(selectFolder));
    }
    private void SelectFolderInExplorer(string path)
    {
        ItemsControl parent=Explorer;
        foreach(string part in path.Split('/'))
        {
            parent.UpdateLayout();
            var node=parent.Items.OfType<ExplorerNode>().FirstOrDefault(x=>x.IsFolder && x.Name==part);
            if(node is null)return;
            if(parent.ItemContainerGenerator.ContainerFromItem(node) is not TreeViewItem item)return;
            item.IsExpanded=true;item.UpdateLayout();
            if(node.FolderPath==path) { item.IsSelected=true;item.BringIntoView();return; }
            parent=item;
        }
    }
    private string? SelectedFile()
    {
        if(Explorer.SelectedItem is ExplorerNode node) return node.Path;
        return currentPath;
    }
    private string TargetFolder()
    {
        if(Explorer.SelectedItem is ExplorerNode node) return node.FolderPath;
        if(newFolderTarget is not null)return newFolderTarget;
        int slash=currentPath?.LastIndexOf('/') ?? -1;
        return slash<0 ? "" : currentPath![..slash];
    }
    private async Task FileOperationAsync(Func<Task> action)
    {
        if(!bridge.Connected) { SetStatus("Connect to the game first.");return; }
        if(localDocument) { SetStatus("Select a game script before managing game files.");return; }
        if(conflict) { SetStatus("Resolve the sync conflict before changing files.");return; }
        if(fileOperationPending)return;
        fileOperationPending=true;
        try
        {
            while(busy) await Task.Delay(50);
            busy=true;Editor.IsReadOnly=true;queuedPath=null;completionRequested=false;completionGeneration++;HideCompletion();
            if(Dirty && currentPath is not null)
            {
                string text=Editor.Text;
                try { await bridge.WriteAsync(currentPath,text,baseline);baseline=text; }
                catch { conflict=true;throw; }
            }
            await action();
        }
        catch(Exception ex) { SetStatus(ex.Message);MessageBox.Show(this,ex.Message,"File operation",MessageBoxButton.OK,MessageBoxImage.Warning); }
        finally { busy=false;fileOperationPending=false;UpdateReadOnly(); }
    }
    private async void NewFileClick(object sender,RoutedEventArgs e)
    {
        if(!bridge.Connected) { SetStatus("Connect to the game first.");return; }
        if(localDocument) { SetStatus("Select a game script before managing game files.");return; }
        string folder=TargetFolder();
        var dialog=new FileNameDialog(this,"New Script","script",folder.Length==0 ? "In: project root" : "In: "+folder);
        if(dialog.ShowDialog()!=true)return;
        await FileOperationAsync(async ()=>
        {
            var result=await bridge.CreateAsync(folder,dialog.FileName);
            if(string.IsNullOrEmpty(result.Path))throw new InvalidOperationException("The bridge did not return the new file path.");
            UpdateExplorer(result);LoadText(result.Path,result.Text ?? "");SetStatus("Created "+result.Path);
        });
    }
    private async void NewFolderClick(object sender,RoutedEventArgs e)
    {
        if(!bridge.Connected) { SetStatus("Connect to the game first.");return; }
        if(localDocument) { SetStatus("Select a game script before managing game folders.");return; }
        string parent=TargetFolder();
        var dialog=new FileNameDialog(this,"New Folder","folder",parent.Length==0 ? "In: project root" : "In: "+parent);
        if(dialog.ShowDialog()!=true)return;
        await FileOperationAsync(async ()=>
        {
            var result=await bridge.CreateFolderAsync(parent,dialog.FileName);
            if(string.IsNullOrEmpty(result.Path))throw new InvalidOperationException("The bridge did not return the new folder path.");
            UpdateExplorer(result,result.Path);SetStatus("Created folder "+result.Path);
        });
    }
    private async void RenameFileClick(object sender,RoutedEventArgs e)
    {
        string? path=SelectedFile();
        if(path is null) { SetStatus("Select a script to rename.");return; }
        var dialog=new FileNameDialog(this,"Rename Script",path.Split('/').Last(),"Script: "+path);
        if(dialog.ShowDialog()!=true)return;
        await FileOperationAsync(async ()=>
        {
            string expected=path==currentPath ? baseline : (await bridge.ReadAsync(path)).Text ?? "";
            var result=await bridge.RenameAsync(path,dialog.FileName,expected);
            if(string.IsNullOrEmpty(result.Path))throw new InvalidOperationException("The bridge did not return the renamed file path.");
            UpdateExplorer(result);
            if(currentPath==path) { currentPath=result.Path;TabTitle.Text=result.Path.Split('/').Last();Breadcrumb.Text=result.Path.Replace("/","  ›  "); }
            SetStatus("Renamed to "+result.Path);
        });
    }
    private async void DeleteFileClick(object sender,RoutedEventArgs e)
    {
        string? path=SelectedFile();
        if(path is null) { SetStatus("Select a script to delete.");return; }
        if(MessageBox.Show(this,$"Delete '{path}'? If it is running, the game will stop it.","Delete Script",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        await FileOperationAsync(async ()=>
        {
            string expected=path==currentPath ? baseline : (await bridge.ReadAsync(path)).Text ?? "";
            var result=await bridge.DeleteAsync(path,expected);UpdateExplorer(result);
            if(currentPath==path)
            {
                currentPath=null;baseline="";loading=true;Editor.Clear();Editor.IsEnabled=false;loading=false;
                RefreshClassMap();
                TabTitle.Text="Welcome";Breadcrumb.Text="Select a script in Explorer";Welcome.Visibility=Visibility.Visible;
            }
            SetStatus("Deleted "+path);
        });
    }
    private void EditorChanged(object? sender,EventArgs e)
    {
        if(referenceHover is not null)ClearReferenceHover();
        if(ClassMap is not null) { classMapTimer.Stop();classMapTimer.Start(); }
        if(loading || (!localDocument && currentPath is null))return;
        lastEdit=DateTime.UtcNow;completionRequested=true;forceCompletion=false;completionAt=lastEdit.AddMilliseconds(150);completionGeneration++;HideCompletion();SetStatus("Editing…");
    }
    private async void Tick(object? sender,EventArgs e)
    {
        if(busy || fileOperationPending || conflict)return;
        if(localDocument)
        {
            if(completionRequested && DateTime.UtcNow>=completionAt)
            {
                completionRequested=false;bool forced=forceCompletion;forceCompletion=false;
                string text=Editor.Text;int caret=Editor.CaretOffset;
                ShowCompletion(localReferences.Complete(language,text,caret,forced),text,caret);
            }
            return;
        }
        if(!bridge.Connected || currentPath is null)return;
        busy=true;
        try
        {
            var now=DateTime.UtcNow;
            if(queuedPath is not null && !Dirty)
            {
                string path=queuedPath;queuedPath=null;var result=await bridge.ReadAsync(path);LoadText(path,result.Text ?? "");
            }
            else if(completionRequested && now>=completionAt && language==bridge.Language)
            {
                completionRequested=false;bool forced=forceCompletion;forceCompletion=false;
                string text=Editor.Text, path=currentPath;int caret=Editor.CaretOffset,generation=completionGeneration;
                BridgeResponse response;
                if(!sdk.TryComplete(text,caret,forced,out response))
                    response=await bridge.CompleteAsync(path,text,caret,forced);
                if(currentPath==path && Editor.Text==text && Editor.CaretOffset==caret && generation==completionGeneration) ShowCompletion(response,text,caret);
            }
            else if(Dirty && now-lastEdit>TimeSpan.FromMilliseconds(400))
            {
                string sent=Editor.Text,path=currentPath,expected=baseline;
                await bridge.WriteAsync(path,sent,expected);
                if(currentPath==path) baseline=sent;
                SetStatus(Dirty ? "Syncing…" : "Synced to game");pollAt=DateTime.UtcNow.AddSeconds(1);
            }
            else if(!Dirty && (sdk.Refreshing || now>=sdk.RefreshAfter))
            {
                if(!sdk.Refreshing)sdk.BeginRefresh();
                string sample=sdk.NextSample+".";
                var metadata=await bridge.CompleteAsync(currentPath,sample,sample.Length,true);
                sdk.AddSample(metadata);
            }
            else if(!Dirty && now>=pollAt)
            {
                pollAt=now.AddSeconds(1);string path=currentPath;var result=await bridge.ReadAsync(path);string live=result.Text ?? "";
                if(path==currentPath && live!=baseline)
                {
                    if(Dirty) { conflict=true;SetStatus("Both the game and IDE changed. Copy your edits, then reload."); }
                    else { int caret=Editor.CaretOffset;LoadText(path,live);Editor.CaretOffset=Math.Min(caret,live.Length);SetStatus("Updated from game"); }
                }
            }
        }
        catch(Exception ex) { conflict=true;HideCompletion();SetStatus(ex.Message+"  Your editor text is preserved; reload or reconnect."); }
        finally { busy=false; }
    }
    private void ShowCompletion(BridgeResponse response,string text,int caret)
    {
        var items=(response.Items ?? []).Select(raw=>JsonSerializer.Deserialize<CompletionItem>(raw)).Where(x=>x is not null).ToList();
        if(items.Count==0) { HideCompletion();return; }
        completionText=text;completionCaret=caret;completionFrom=response.From;
        CompletionList.ItemsSource=items;CompletionList.SelectedIndex=0;
        if(!Editor.TextArea.TextView.VisualLinesValid)return;
        var view=Editor.TextArea.TextView;
        Point box=view.GetVisualPosition(Editor.TextArea.Caret.Position,VisualYPosition.LineBottom)-view.ScrollOffset;
        box=view.TranslatePoint(box,Editor);
        CompletionPopup.HorizontalOffset=box.X;CompletionPopup.VerticalOffset=box.Y;CompletionPopup.IsOpen=true;
    }
    private void HideCompletion() { CompletionPopup.IsOpen=false; }
    private void CompletionSelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        CompletionDocumentation.Text=(CompletionList.SelectedItem as CompletionItem)?.Doc ?? "";
        if(CompletionList.SelectedItem is not null)CompletionList.ScrollIntoView(CompletionList.SelectedItem);
    }
    private void CompletionDoubleClick(object sender,MouseButtonEventArgs e) => AcceptCompletion();
    private void AcceptCompletion()
    {
        if(CompletionList.SelectedItem is not CompletionItem item || Editor.Text!=completionText || Editor.CaretOffset!=completionCaret)return;
        if(completionFrom<0 || completionFrom>completionCaret || completionCaret>Editor.Text.Length)return;
        string insert=item.Insert ?? item.Label;
        Editor.Select(completionFrom,completionCaret-completionFrom);Editor.SelectedText=insert;
        int caret=completionFrom+Math.Clamp(item.Caret<0 ? insert.Length : item.Caret,0,insert.Length);
        Editor.Select(caret,0);
        Editor.CaretOffset=caret;
        completionRequested=false;HideCompletion();Editor.Focus();
    }
    private void EditorKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { completionGeneration++;completionRequested=true;forceCompletion=true;completionAt=DateTime.MinValue;e.Handled=true;return; }
        if(e.Key==Key.Escape) { completionGeneration++;completionRequested=false;HideCompletion();e.Handled=true;return; }
        if(CompletionPopup.IsOpen && e.Key is (Key.Down or Key.Up))
        {
            int delta=e.Key==Key.Down ? 1 : -1;CompletionList.SelectedIndex=Math.Clamp(CompletionList.SelectedIndex+delta,0,CompletionList.Items.Count-1);e.Handled=true;
        }
        else if(CompletionPopup.IsOpen && e.Key is (Key.Enter or Key.Tab)) { AcceptCompletion();e.Handled=true; }
        else if(CompletionPopup.IsOpen && e.Key is (Key.Left or Key.Right or Key.Home or Key.End)) HideCompletion();
        if(!e.Handled && e.Key==Key.Enter && Keyboard.Modifiers==ModifierKeys.None && Editor.IsEnabled && !Editor.IsReadOnly)
        {
            var edit=BraceEditing.NewLine(Editor.Text,Editor.CaretOffset,Editor.SelectionLength);
            if(edit is not null) { ApplyBraceEdit(edit);e.Handled=true; }
        }
    }
    private void EditorTextEntering(object? sender,TextCompositionEventArgs e)
    {
        if(!Editor.IsEnabled || Editor.IsReadOnly)return;
        BraceEditing.Edit? edit=e.Text switch
        {
            "{"=>BraceEditing.OpeningBrace(Editor.Text,Editor.SelectionStart,Editor.SelectionLength),
            "}"=>BraceEditing.ClosingBrace(Editor.Text,Editor.CaretOffset,Editor.SelectionLength),
            _=>null
        };
        if(edit is not null) { ApplyBraceEdit(edit);e.Handled=true; }
    }
    private void ApplyBraceEdit(BraceEditing.Edit edit)
    {
        if(edit.Length!=0 || edit.Text.Length!=0)Editor.Document.Replace(edit.Offset,edit.Length,edit.Text);
        Editor.Select(edit.CaretOffset,edit.SelectionLength);
        if(edit.SelectionLength==0)Editor.CaretOffset=edit.CaretOffset;
    }
    private void EditorMouseDown(object sender,MouseButtonEventArgs e)
    {
        completionGeneration++;completionRequested=false;HideCompletion();
        if(e.ChangedButton!=MouseButton.Left || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))return;
        if(!bridge.Connected) { SetStatus("Connect to a game world to use the reference.");e.Handled=true;return; }
        if(language!=bridge.Language) { SetStatus("Reference lookup follows the connected world's language.");e.Handled=true;return; }
        var target=ReferenceAt(e.GetPosition(Editor));
        if(target is null)SetStatus("No unlocked in-game reference found for that symbol.");
        else _=OpenReferenceAsync(target.Id);
        e.Handled=true;
    }
    private ReferenceLookup.Target? ReferenceAt(Point point)
    {
        var position=Editor.GetPositionFromPoint(point);
        if(position is null || position.Value.Line<1 || position.Value.Line>Editor.Document.LineCount)return null;
        var line=Editor.Document.GetLineByNumber(position.Value.Line);
        int offset=Editor.Document.GetOffset(position.Value.Line,Math.Clamp(position.Value.Column,1,line.Length+1));
        var target=ReferenceLookup.FindTarget(Editor.Text,offset,Reference.Entries);
        if(target is null && offset>line.Offset)target=ReferenceLookup.FindTarget(Editor.Text,offset-1,Reference.Entries);
        var view=Editor.TextArea.TextView;
        if(target is null || !view.VisualLinesValid)return null;
        Point local=Editor.TranslatePoint(point,view);
        var segment=new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset=target.Start,Length=target.Length };
        return BackgroundGeometryBuilder.GetRectsForSegment(view,segment,false).Any(rect=>rect.Contains(local)) ? target : null;
    }
    private void UpdateReferenceHover(Point point)
    {
        if(referenceHover is null || !Editor.IsMouseOver || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || !bridge.Connected || language!=bridge.Language)
        { ClearReferenceHover();return; }
        var target=ReferenceAt(point);
        if(target is null) { ClearReferenceHover();return; }
        referenceHover.Set(target.Start,target.Length);
        Editor.TextArea.TextView.Cursor=Cursors.Hand;
    }
    private void ClearReferenceHover()
    {
        referenceHover?.Clear();
        if(Editor?.TextArea?.TextView is not null)Editor.TextArea.TextView.ClearValue(FrameworkElement.CursorProperty);
    }
    private void EditorSelectionChanged(object? sender,EventArgs e)
    {
        if(CompletionPopup.IsOpen && (Editor.Text!=completionText || Editor.CaretOffset!=completionCaret))HideCompletion();
    }
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(Dirty && MessageBox.Show(this,localDocument ? "This local file has unsaved changes. Close anyway?" : "Some edits have not synced. Close anyway?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)e.Cancel=true;
    }
}
