namespace SMSMais.Data.Entities.Alertas;

/// <summary>
/// Telefone que recebe no WhatsApp os avisos de erro da plataforma (robô parado, sincronismo que
/// falhou, erro 500 novo…). É a ÚNICA lista de aviso de erro/falha da instância: a que existia
/// por integração (<c>telefonesNotificacao</c> na credencial) foi absorvida em 24/09/2026 pela
/// migration <c>UnificarTelefonesAvisos</c>.
/// </summary>
public sealed class AlertaDestinatario
{
    public Guid Id { get; set; }

    /// <summary>Só dígitos, com DDD (10 a 13). Único.</summary>
    public string Telefone { get; set; } = string.Empty;

    /// <summary>Quem é (para a tela; não vai na mensagem).</summary>
    public string? Nome { get; set; }

    /// <summary>Desligado = continua na lista, mas não recebe (férias, troca de plantão).</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Quem escreve deste número conversa com o Agente IA do servidor (ADR-0068). Desligar corta
    /// na hora: o manipulador do webhook relê a lista a cada mensagem.
    /// </summary>
    public bool AgenteIa { get; set; }

    /// <summary>
    /// Usuário que o telefone REPRESENTA no agente — é dele a identidade carimbada e é por ele que
    /// o motor decide acesso total × somente leitura (<c>AIENGINE_ADMIN_USUARIO_IDS</c>).
    /// Obrigatório com <see cref="AgenteIa"/> ligado; só o próprio usuário se vincula.
    /// </summary>
    public Guid? AgenteUsuarioId { get; set; }
    public Usuario? AgenteUsuario { get; set; }

    public DateTime CriadoEm { get; set; }
    public string? CriadoPor { get; set; }
}
