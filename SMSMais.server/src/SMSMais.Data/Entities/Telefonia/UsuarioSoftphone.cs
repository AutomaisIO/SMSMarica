namespace SMSMais.Data.Entities.Telefonia;

/// <summary>
/// Ramal de softphone de um usuário do painel. O ramal em si (bloco SIP, segredo) vive na VM de
/// telefonia, no <b>Automais.Pabx</b>; aqui fica só o vínculo usuário ↔ número e o nome que
/// aparece no visor de quem recebe a ligação.
/// </summary>
/// <remarks>
/// <para><b>O segredo SIP não fica neste banco.</b> O navegador do próprio usuário recebe a
/// credencial na hora, por <c>GET /identidade/me/softphone/credencial</c>, que a busca no Pabx.
/// Por isso não é CPF + senha: o digest SIP exige a senha em claro no Asterisk, e aqui só
/// existe o hash.</para>
/// <para>Uma linha por usuário (no máximo um softphone) e um usuário por ramal.</para>
/// </remarks>
public class UsuarioSoftphone
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Número do ramal no Pabx (faixa de softphone, ex.: 3000–3999).</summary>
    public string Ramal { get; set; } = string.Empty;

    /// <summary>Nome no visor de quem recebe (caller-id). Sem quebra de linha nem [ ] ; " &lt; &gt; =.</summary>
    public string NomeExibicao { get; set; } = string.Empty;

    /// <summary>Desligado: o ramal continua reservado ao usuário, mas não registra nem recebe credencial.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
