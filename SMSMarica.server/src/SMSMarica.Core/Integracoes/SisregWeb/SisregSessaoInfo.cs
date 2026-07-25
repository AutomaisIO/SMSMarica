namespace SMSMarica.Core.Integracoes.SisregWeb;

/// <summary>
/// Identidade da sessão logada no SISREG, lida da barra superior da home
/// (<c>/cgi-bin/index</c>): "Operador: … Perfil: … Unidade: NOME (CNES)".
///
/// <para>É o que permite o <b>double-check</b> de unidade: antes de mapear ou varrer,
/// conferimos que a unidade da sessão do SISREG é a mesma unidade selecionada no
/// sistema. Sem isso, uma credencial trocada mapearia a agenda da unidade errada.</para>
/// </summary>
public sealed record SisregSessaoInfo(
    string Operador,
    string Perfil,
    string UnidadeNome,
    string? Cnes);
