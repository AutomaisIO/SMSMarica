namespace SMSMarica.Core.Procedimentos.Dtos;

public sealed record ProcedimentoSigtapDto(
    Guid Id,
    string Codigo,
    string Nome,
    string Grupo,
    string Subgrupo,
    string Forma,
    string Descricao,
    bool Ativo,
    DateOnly CompetenciaInicio,
    DateOnly? CompetenciaFim);
