using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Medicos.Assinatura;

/// <summary>Rubrica visual (imagem) de assinatura do médico + formato escolhido.</summary>
public sealed record AssinaturaMedicoDto(
    Guid MedicoId,
    string ImagemBase64,
    string ContentType,
    FormatoAssinaturaMedico Formato,
    DateTime AtualizadoEm);

/// <summary>Payload de upload/atualização da rubrica (a imagem já vem enquadrada do client).</summary>
public sealed record SalvarAssinaturaMedicoRequest(
    string ImagemBase64,
    string ContentType,
    FormatoAssinaturaMedico Formato);

/// <summary>
/// Modo de assinatura de laudo do médico (ADR-0061). <see cref="Configurado"/> = false quando
/// ninguém escolheu ainda e vale o padrão (<see cref="ModoAssinaturaMedico.Desktop"/>).
/// </summary>
public sealed record ModoAssinaturaMedicoDto(
    Guid MedicoId,
    ModoAssinaturaMedico Modo,
    bool Configurado,
    DateTime? AtualizadoEm);

/// <summary>Payload da troca de modo de assinatura.</summary>
public sealed record DefinirModoAssinaturaMedicoRequest(ModoAssinaturaMedico Modo);
