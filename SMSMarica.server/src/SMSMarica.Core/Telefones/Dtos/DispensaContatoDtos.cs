using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Telefones.Dtos;

/// <summary>Uma opção de motivo para a recepção escolher (o front monta o select com isto).</summary>
public sealed record MotivoDispensaContatoDto(
    MotivoDispensaContato Motivo,
    string Rotulo,
    /// <summary>Depois de dispensar por este motivo, resultado/laudo ainda vão por WhatsApp?</summary>
    bool PermiteEnvio,
    /// <summary>Frase mostrada ao operador explicando o efeito da escolha.</summary>
    string Consequencia,
    /// <summary>Descrição em texto livre é obrigatória para este motivo?</summary>
    bool ExigeDescricao);

/// <summary>Pedido da recepção para registrar a dispensa de verificação do contato.</summary>
public sealed record RegistrarDispensaContatoRequest(
    Guid PacienteId,
    MotivoDispensaContato Motivo,
    string? MotivoDescricao,
    /// <summary>O operador afirma que informou o paciente e ele concordou. Sem isso, 400.</summary>
    bool PacienteCiente);

/// <summary>Pedido para derrubar a dispensa na mão (ex.: paciente voltou com celular).</summary>
public sealed record RevogarDispensaContatoRequest(string? Motivo);

/// <summary>Dispensa ATIVA de um paciente. Ausência (null) = o gate de verificado vale normal.</summary>
public sealed record DispensaContatoDto(
    Guid Id,
    Guid PacienteId,
    MotivoDispensaContato Motivo,
    string MotivoRotulo,
    string? MotivoDescricao,
    /// <summary>Texto pronto para a tela: o rótulo, ou a descrição quando o motivo é "Outro".</summary>
    string MotivoTexto,
    bool PermiteEnvio,
    DateTime CriadoEm,
    Guid? CriadoPor,
    string? CriadoPorNome);
