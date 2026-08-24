namespace SMSMais.Data.Entities;

/// <summary>
/// Token de download público, de uso único e com validade (dias configuráveis).
/// Usado para enviar ao paciente (ex.: WhatsApp) o link do exame completo sem exigir
/// login. Após baixado uma vez — ou expirado — o link não serve mais (o cidadão passa
/// a acessar pelo app). O <see cref="Id"/> é o token que vai na URL.
/// </summary>
public class DownloadToken
{
    public Guid Id { get; set; }

    /// <summary>Tipo de conteúdo (ex.: "exame-completo"). Permite outros usos no futuro.</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Id do recurso referenciado (ex.: SolicitacaoExame.Id para "exame-completo").</summary>
    public Guid ReferenciaId { get; set; }

    public DateTime ExpiraEm { get; set; }

    /// <summary>Tentativas de CPF já erradas. Na 3ª o link é queimado (<see cref="ExpiraEm"/>
    /// antecipado) e a recepção precisa reenviar.</summary>
    public int TentativasCpf { get; set; }

    /// <summary>Liberação de curta duração emitida quando o CPF do titular confere. O GET que
    /// baixa o arquivo precisa apresentá-la — é o que permite manter o download em streaming
    /// (PDF de mamografia passa de 50 MB) em vez de trafegar tudo pela resposta de um POST.</summary>
    public Guid? Liberacao { get; set; }
    public DateTime? LiberadoEm { get; set; }

    /// <summary>Preenchido no 1º download bem-sucedido — a partir daí o link é inválido.</summary>
    public DateTime? UsadoEm { get; set; }
    public string? UsadoIp { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
