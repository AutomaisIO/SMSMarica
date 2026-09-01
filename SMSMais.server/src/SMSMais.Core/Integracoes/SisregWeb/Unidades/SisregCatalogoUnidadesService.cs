using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Integracoes.SisregWeb.Unidades;

/// <summary>
/// Descobre no SISREG <b>todas</b> as unidades que a credencial enxerga e reconcilia com o nosso
/// cadastro: cria as que faltam e preenche o CNES das que já existiam sem ele.
///
/// <para><b>Por que isto existe:</b> o "sincroniza tudo" do mapeamento nasceu olhando só para
/// dentro — as unidades que alguém já tinha mapeado à mão. Numa instalação com 43 unidades
/// cadastradas e 6 mapeadas, o botão tocava 6. Perguntar ao SISREG quais unidades existem custa
/// <b>uma requisição</b> (o combo do <c>cons_agendas</c>), então a descoberta é praticamente de
/// graça — o caro sempre foi mapear médico, não descobrir unidade.</para>
/// </summary>
public interface ISisregCatalogoUnidadesService
{
    /// <summary>
    /// Lista as unidades do SISREG. <b>Uma requisição.</b> Lança <see cref="ValidacaoException"/>
    /// quando não consegue ler o combo — vazio aqui é sessão caída ou tela mudada, nunca
    /// "a rede não tem unidades".
    /// </summary>
    Task<IReadOnlyList<ConsAgendasUpsParser.UnidadeSisreg>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista e reconcilia com o cadastro local. Não apaga nem inativa nada: unidade que existe
    /// aqui e não aparece no SISREG é deixada em paz (pode ser unidade de outro fluxo, ou o perfil
    /// da credencial pode ter mudado de escopo).
    /// </summary>
    Task<ReconciliacaoUnidadesSisreg> ReconciliarAsync(CancellationToken cancellationToken = default);
}

/// <summary>Resultado da reconciliação — o "quantas unidades tem, quantas entraram" da tela.</summary>
/// <param name="NoSisreg">Unidades que a credencial enxerga.</param>
/// <param name="Criadas">Criadas agora no cadastro local.</param>
/// <param name="CnesPreenchido">Já existiam por nome e ganharam o CNES.</param>
/// <param name="JaExistiam">Já casavam por CNES — nada a fazer.</param>
/// <param name="Requisicoes">Custo em requisições ao SISREG.</param>
/// <param name="CriadasNomes">Nomes das criadas, para a mensagem e o rastreio.</param>
/// <param name="CriadasIds">Ids das criadas — o lote marca o item como "nasceu agora".</param>
/// <param name="CnesNoSisreg">
/// CNES que a credencial enxerga. É o recorte "de lá para cá" do lote: unidade que existe só aqui
/// (fechada, de outro fluxo, ou fora do escopo da credencial) não é ida ao SISREG para descobrir
/// que não está lá — seria uma requisição por unidade, toda rodada, para não achar nada.
/// </param>
public sealed record ReconciliacaoUnidadesSisreg(
    int NoSisreg,
    int Criadas,
    int CnesPreenchido,
    int JaExistiam,
    int Requisicoes,
    IReadOnlyList<string> CriadasNomes,
    IReadOnlySet<Guid> CriadasIds,
    IReadOnlySet<string> CnesNoSisreg);

public sealed class SisregCatalogoUnidadesService(
    SmsMaisDbContext db,
    ISisregWebSessao sessao,
    ILogger<SisregCatalogoUnidadesService> logger) : ISisregCatalogoUnidadesService
{
    private const string CaminhoFormulario = "/cgi-bin/cons_agendas";

    public async Task<IReadOnlyList<ConsAgendasUpsParser.UnidadeSisreg>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        // O formulário vem inteiro num GET — o combo `ups` já chega preenchido no HTML servido.
        var html = await sessao.GetAsync(CaminhoFormulario, null, cancellationToken, ParecemSemCombo);

        var unidades = ConsAgendasUpsParser.Ler(html);
        if (unidades.Count == 0)
        {
            throw new ValidacaoException(
                "sisreg.unidades_nao_listadas",
                "O SISREG não devolveu a lista de unidades. Isso normalmente significa que a sessão "
                + "do operador foi derrubada (o SISREG aceita uma sessão por operador) ou que o "
                + "acesso está bloqueado por CAPTCHA. Teste a credencial do SISREG e tente de novo.");
        }

        return unidades;
    }

    public async Task<ReconciliacaoUnidadesSisreg> ReconciliarAsync(
        CancellationToken cancellationToken = default)
    {
        var doSisreg = await ListarAsync(cancellationToken);

        var locais = await db.Unidades.ToListAsync(cancellationToken);
        var porCnes = locais
            .Where(u => !string.IsNullOrWhiteSpace(u.Cnes))
            .GroupBy(u => SoDigitos(u.Cnes), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Só entram no casamento por nome as que NÃO têm CNES: preencher CNES é decisão de
        // identidade, e sobrescrever um CNES já cadastrado por semelhança de nome trocaria a
        // unidade de lugar sem ninguém pedir.
        var porNomeSemCnes = locais
            .Where(u => string.IsNullOrWhiteSpace(u.Cnes))
            .GroupBy(u => Chave(u.Nome), StringComparer.Ordinal)
            .Where(g => g.Count() == 1) // nome ambíguo não casa: melhor criar do que casar errado
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);

        var agora = DateTime.UtcNow;
        var criadas = new List<string>();
        var criadasIds = new HashSet<Guid>();
        var cnesPreenchido = 0;
        var jaExistiam = 0;

        foreach (var (cnes, nome) in doSisreg)
        {
            if (porCnes.ContainsKey(cnes))
            {
                jaExistiam++;
                continue;
            }

            var chaveNome = Chave(nome);
            if (porNomeSemCnes.TryGetValue(chaveNome, out var semCnes))
            {
                semCnes.Cnes = cnes;
                semCnes.AtualizadoEm = agora;
                porCnes[cnes] = semCnes;
                porNomeSemCnes.Remove(chaveNome);
                cnesPreenchido++;
                logger.LogInformation(
                    "SISREG_UNIDADES: CNES {Cnes} preenchido em {Unidade} por casamento de nome.",
                    cnes, semCnes.Nome);
                continue;
            }

            var nova = new Unidade
            {
                Id = Guid.CreateVersion7(),
                // Caixa alta é como o SISREG (e o resto da importação) grava — mantém o cadastro
                // coerente com as unidades que já nascem da importação de arquivo.
                Nome = nome,
                Cnes = cnes,
                Ativo = true,
                Externa = false,
                CriadoEm = agora,
            };
            db.Unidades.Add(nova);
            porCnes[cnes] = nova;
            criadas.Add(nome);
            criadasIds.Add(nova.Id);
        }

        if (criadas.Count > 0 || cnesPreenchido > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "SISREG_UNIDADES: {NoSisreg} no SISREG — {Criadas} criadas, {Backfill} com CNES preenchido, {Iguais} já existiam.",
                doSisreg.Count, criadas.Count, cnesPreenchido, jaExistiam);
        }

        return new ReconciliacaoUnidadesSisreg(
            doSisreg.Count, criadas.Count, cnesPreenchido, jaExistiam, Requisicoes: 1, criadas, criadasIds,
            doSisreg.Select(u => u.Cnes).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Resposta sem o combo de unidades = sessão suspeita. Mesmo papel do <c>&lt;ROOT/&gt;</c>
    /// vazio no AJAX do mapeamento: a sessão derrubada devolve uma página que parece válida, e sem
    /// este palpite o relogin nunca dispararia.
    ///
    /// <para><b>Menos a tela de CAPTCHA</b>, que também não tem combo: ali relogar não resolve
    /// (a sessão está boa, o bloqueio é do operador) e ainda gasta uma requisição a mais no
    /// exato momento em que o SISREG está contando. Deixa passar para a checagem de CAPTCHA da
    /// sessão, que levanta o erro certo.</para>
    /// </summary>
    private static bool ParecemSemCombo(string html) =>
        !SisregHomeParser.ExigeCaptcha(html) && ConsAgendasUpsParser.Ler(html).Count == 0;

    private static string SoDigitos(string? valor) =>
        new([.. (valor ?? string.Empty).Where(char.IsDigit)]);

    /// <summary>
    /// Chave de comparação de nome: sem acento, sem pontuação, maiúscula, espaço colapsado. O
    /// SISREG grava sem acento e a nossa base nem sempre — sem normalizar, "UNIDADE BASICA SAUDE"
    /// e "Unidade Básica de Saúde" nunca casariam.
    /// </summary>
    private static string Chave(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return string.Empty;

        var decomposto = nome.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        var espacoPendente = false;

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(c))
            {
                if (espacoPendente && sb.Length > 0) sb.Append(' ');
                espacoPendente = false;
                sb.Append(c);
            }
            else
            {
                espacoPendente = true;
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
