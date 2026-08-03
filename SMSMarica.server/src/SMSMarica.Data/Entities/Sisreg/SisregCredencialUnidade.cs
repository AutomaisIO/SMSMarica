namespace SMSMarica.Data.Entities.Sisreg;

/// <summary>
/// Credencial de operador do SISREG <b>por unidade</b>. Cada unidade da secretaria
/// cadastra o usuário de coordenação que enxerga as agendas daquela unidade (recebe TXT,
/// consulta agendas, etc.) — é essa credencial que o serviço usa ao falar com o SISREG
/// no contexto da unidade.
///
/// <para><b>Por que por unidade:</b> o perfil do operador do SISREG enxerga apenas a
/// unidade dele (o select de unidade executante traz um CNES só). Uma credencial global
/// não consegue varrer a secretaria inteira — ver <c>Automais.SISREG/docs/APRENDIZADOS.md</c>.</para>
///
/// <para><b>Senha:</b> cifrada em repouso (IProtetorSegredos) e write-only na API — a tela
/// mostra só o <see cref="Usuario"/>. Trocar usuário/senha é fluxo próprio (modal), com
/// autenticação real contra o SISREG antes de gravar.</para>
///
/// <para>Quando a unidade não tem credencial própria, o motor cai na credencial global
/// (store de Integrações, provedor <c>sisreg</c>) para não quebrar o que já roda em produção.</para>
/// </summary>
public class SisregCredencialUnidade
{
    public Guid Id { get; set; }

    /// <summary>Unidade dona da credencial. Uma credencial por unidade (índice único).</summary>
    public Guid UnidadeId { get; set; }

    /// <summary>Usuário do SISREG, no formato <c>NNN-NOME</c>. Público — aparece na tela.
    /// Não usar login de operador real como exemplo, aqui nem no placeholder da tela.</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Senha cifrada em repouso. Nunca sai da API.</summary>
    public string SenhaCifrada { get; set; } = string.Empty;

    /// <summary>
    /// CNES confirmado pelo SISREG na última autenticação bem-sucedida. Serve para o
    /// double-check: a unidade da sessão do SISREG tem que bater com a unidade selecionada.
    /// </summary>
    public string? CnesConfirmado { get; set; }

    /// <summary>Nome da unidade como o SISREG a chama (barra "Unidade:" da home logada).</summary>
    public string? UnidadeSisregNome { get; set; }

    /// <summary>Última autenticação bem-sucedida contra o SISREG.</summary>
    public DateTime? ValidadoEm { get; set; }

    /// <summary>Credencial habilitada. Quando false, o motor não a usa (cai no fallback global).</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
