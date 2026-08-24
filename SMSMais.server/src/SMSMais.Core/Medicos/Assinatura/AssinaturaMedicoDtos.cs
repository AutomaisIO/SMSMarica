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
