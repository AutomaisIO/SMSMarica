using Microsoft.EntityFrameworkCore;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Comum;

/// <summary>
/// A <b>família</b> de um procedimento do SISREG: o conjunto de nomes que, na fila e na escala,
/// falam do mesmo assunto.
///
/// <para><b>Por que existe.</b> O SISREG publica a escala em <i>grupos</i> (código de 7 dígitos
/// terminado em <c>000</c>, ex.: <c>0229000 GRUPO - ULTRASONOGRAFIA</c>) que se expandem em
/// <i>itens</i> no agendamento (<c>0229010 ULTRASONOGRAFIA TRANSVAGINAL</c>). A fila de espera só
/// traz o NOME do procedimento — nunca o código. Casar fila com oferta por igualdade exata dá zero
/// justamente nos procedimentos de fila maior; casar por <c>Contains</c> arrasta filas alheias
/// ("CONSULTA EM CARDIOLOGIA" arrastaria "CONSULTA EM CARDIOLOGIA - PEDIATRIA"). A família é o
/// meio-termo defensável: nome exato + o outro lado do grupo, também por nome exato.</para>
///
/// <para>Regra medida em produção (Ofertas, 10/09/2026) e usada pela tela de Ofertas e pelo
/// simulador de estratégias de fila — <b>uma régua só</b>, para os dois mostrarem o mesmo número.</para>
/// </summary>
public static class FamiliaProcedimentoSisreg
{
    /// <summary>Código do SISREG tem 7 dígitos; terminado em <c>000</c> é GRUPO.</summary>
    public static bool EhCodigoDeGrupo(string codigo) =>
        codigo.Length == 7 && codigo.EndsWith("000", StringComparison.Ordinal);

    /// <summary>O grupo que cobre o item: mesmo prefixo de 4 dígitos + <c>000</c>. Nulo se o
    /// código não tiver a forma do SISREG.</summary>
    public static string? GrupoDoCodigo(string codigo) =>
        codigo.Length == 7 && codigo.All(char.IsAsciiDigit) ? codigo[..4] + "000" : null;

    /// <summary>
    /// Os nomes de fila/escala que pertencem à família de <paramref name="nome"/>.
    ///
    /// <para>Sem código (procedimento que só existe na fila), a família é o nome sozinho. Com
    /// código de grupo, entram os nomes de todas as escalas e de todas as marcações do mesmo
    /// prefixo. Com código de item, entra o nome do grupo que o cobre.</para>
    /// </summary>
    public static async Task<List<string>> NomesDaFamiliaAsync(
        SmsMaisDbContext db, string nome, string? codigo, CancellationToken ct)
    {
        var nomes = new HashSet<string>(StringComparer.Ordinal) { nome };
        if (string.IsNullOrEmpty(codigo) || GrupoDoCodigo(codigo) is not { } grupo) return [.. nomes];

        if (EhCodigoDeGrupo(codigo))
        {
            // Vaga de grupo: quem pediu qualquer item do grupo espera por ela.
            var prefixo = codigo[..4];
            nomes.UnionWith(await db.SisregEscalas.AsNoTracking()
                .Where(e => e.ProcedimentoCodigo.StartsWith(prefixo))
                .Select(e => e.ProcedimentoNome).Distinct().ToListAsync(ct));
            nomes.UnionWith(await db.Solicitacoes.AsNoTracking()
                .Where(s => s.ProcedimentoCodigoSisreg != null
                    && s.ProcedimentoCodigoSisreg.StartsWith(prefixo)
                    && s.ProcedimentoTexto != null)
                .Select(s => s.ProcedimentoTexto!).Distinct().ToListAsync(ct));
        }
        else
        {
            // Vaga de item: quem pediu o grupo inteiro também serve.
            nomes.UnionWith(await db.SisregEscalas.AsNoTracking()
                .Where(e => e.ProcedimentoCodigo == grupo)
                .Select(e => e.ProcedimentoNome).Distinct().ToListAsync(ct));
        }

        return [.. nomes];
    }

    /// <summary>
    /// Os códigos de escala que pertencem à família: o próprio, e — para grupo — todos os itens do
    /// prefixo; para item — o grupo que o cobre. Serve para recortar <c>sisreg_escala</c> e
    /// <c>solicitacao.procedimento_codigo_sisreg</c> sem depender do nome.
    /// </summary>
    public static (string Prefixo, bool Grupo)? RecorteDeCodigo(string? codigo)
    {
        if (string.IsNullOrEmpty(codigo) || GrupoDoCodigo(codigo) is null) return null;
        return (codigo[..4], EhCodigoDeGrupo(codigo));
    }
}
