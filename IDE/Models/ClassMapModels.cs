namespace CodeMarket.IDE.Models;

public sealed record ClassMapMember(string Name, string Kind, string Detail, int Offset);
public sealed record ClassMapType(string Name, string Kind, string Bases, int Offset, List<ClassMapMember> Members);
