namespace SMSMais.Data.Entities;

/// <summary>
/// "Magic-link" de login do cidadão: token de uso único e validade curta (dias
/// configuráveis) enviado por WhatsApp. Ao ser trocado, abre uma sessão normal do
/// cidadão (como o OTP) — 1 clique, sem digitar código (pensado para idosos). O
/// <see cref="Id"/> é o token que vai na URL <c>app.smsmarica.online/entrar/{id}</c>.
/// Não persistimos nome (vem do hub FHIR na troca).
/// </summary>
public class CidadaoLoginLink
{
    public Guid Id { get; set; }

    /// <summary>Paciente (fhir.patient) que o link autentica.</summary>
    public Guid PatientId { get; set; }

    /// <summary>CPF (dígitos) do paciente — usado ao abrir a sessão.</summary>
    public string Cpf { get; set; } = string.Empty;

    /// <summary>Rota relativa de destino no app após logar (ex.: "/exames").</summary>
    public string? Destino { get; set; }

    /// <summary>Solicitação de exame que originou o link (notificação de agendamento).
    /// Quando presente, o USO do link confirma a presença do paciente. Null em links avulsos.</summary>
    public Guid? SolicitacaoId { get; set; }

    public DateTime ExpiraEm { get; set; }

    /// <summary>Exige que o portador confirme o CPF do titular antes de virar sessão. Ligado nas
    /// finalidades que carregam resultado clínico (exame liberado, laudo pronto): quem tiver o link
    /// nas mãos por engano não entra no prontuário alheio. Confirmação de agendamento segue em 1
    /// clique — não expõe resultado e depende de ser instantânea.</summary>
    public bool ExigeConfirmacaoCpf { get; set; }

    /// <summary>Tentativas de CPF já erradas. Na 3ª o link é queimado (<see cref="ExpiraEm"/>
    /// antecipado) e a recepção precisa reenviar.</summary>
    public int TentativasCpf { get; set; }

    /// <summary>Preenchido na 1ª troca por sessão — a partir daí o link é inválido.</summary>
    public DateTime? UsadoEm { get; set; }
    public string? UsadoIp { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
