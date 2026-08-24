namespace SMSMais.Core.Cidadao.Dtos;

/// <summary>Status do consentimento do cidadão + o texto vigente do termo.</summary>
public sealed record ConsentimentoStatusDto(
    string Versao,
    string Texto,
    bool Aceito,
    DateTime? AceitoEm);
