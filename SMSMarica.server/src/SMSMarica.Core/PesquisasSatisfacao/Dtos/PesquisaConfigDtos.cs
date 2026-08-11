namespace SMSMarica.Core.PesquisasSatisfacao.Dtos;

/// <summary>
/// Configuração da pesquisa de uma unidade, para a aba na tela da unidade.
///
/// <para><b>Só três unidades têm atendimento no hub hoje</b> — Conde (2266733), UPA Inoã
/// (7164440) e Santa Rita (2266792). Nas demais o disparo não teria evento para acontecer, e a
/// tela avisa isso. Não é checagem automática: o hub é serviço autônomo (ADR-0010) e o servidor
/// não faz agregado nele. Se outra unidade passar a ter atendimento, o aviso precisa mudar
/// junto — está em `PesquisaSatisfacaoAba.tsx`.</para>
/// </summary>
public sealed record PesquisaConfigDto(
    Guid UnidadeId,
    string UnidadeNome,
    string? Cnes,
    bool EnvioWhatsAppAtivo,
    string? LinkResponder,
    string? LinkPainel,
    int HorasAposAtendimento,
    DateTime? AtualizadoEm);

/// <summary>Salvamento da aba. Sem o link de resposta, o disparo não pode ser ligado.</summary>
public sealed record SalvarPesquisaConfigRequest(
    bool EnvioWhatsAppAtivo,
    string? LinkResponder,
    string? LinkPainel,
    int HorasAposAtendimento);

/// <summary>Números do painel da unidade. Ver <see cref="PesquisaPainelDto.Clicadas"/>.</summary>
public sealed record PesquisaPainelDto(
    int Enviadas,
    int Entregues,
    int Vistas,
    /// <summary>Cliques no link. <b>Não é "respondidas"</b> — quem sabe isso é a AvanteSocial.</summary>
    int Clicadas,
    double? HorasMedianasAteClique,
    IReadOnlyList<PesquisaPerfilDto> Perfil);

/// <summary>Perfil de quem clicou — sexo e faixa etária, sem tocar em resposta.</summary>
public sealed record PesquisaPerfilDto(string Sexo, string FaixaEtaria, int Cliques);
