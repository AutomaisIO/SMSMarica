using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Recupera o profissional EXECUTANTE das solicitações já importadas, relendo a linha crua do TXT
/// que ficou guardada em <c>Solicitacao.RawSisreg</c>.
///
/// <para><b>Por que dá para fazer sem falar com o SISREG:</b> o <see cref="AgendaTxtParser"/> sempre
/// leu o CPF (coluna 4) e o nome (coluna 5) do executante para montar o mapeamento de profissionais
/// — mas o valor nunca era gravado na solicitação. O RAW, por outro lado, foi guardado desde o
/// começo. Medido em 04/09/2026: <b>21.228 das 21.235</b> solicitações com RAW são a linha do TXT
/// com 38 campos, e o CPF é válido em <b>100%</b> delas (271 executantes distintos, todos já
/// conhecidos em <c>sisreg_profissional_unidade</c>). Então o passado inteiro sai de graça: zero
/// requisição, zero risco de CAPTCHA, zero disputa pela sessão única do operador.</para>
///
/// <para><b>Idempotente</b> — só toca em linha com o campo vazio. Pode rodar quantas vezes precisar.
/// As poucas linhas cujo RAW é JSON (caminho pontual do <c>cons_agendas</c>, onde o profissional era
/// o filtro da consulta e não uma coluna do resultado) simplesmente não têm o dado e são contadas
/// como ignoradas, não como erro.</para>
/// </summary>
public interface IBackfillExecutanteService
{
    Task<BackfillExecutanteResultado> ExecutarAsync(CancellationToken cancellationToken = default);
}

/// <param name="Examinadas">Solicitações sem executante e com RAW que foram lidas.</param>
/// <param name="Preenchidas">Quantas ganharam CPF do executante.</param>
/// <param name="SemDadoNoRaw">RAW existe mas não carrega o executante (JSON do caminho pontual).</param>
/// <param name="Pendentes">Continuam sem executante depois desta passada.</param>
public sealed record BackfillExecutanteResultado(
    int Examinadas, int Preenchidas, int SemDadoNoRaw, int Pendentes, string Mensagem);

public sealed class BackfillExecutanteService(
    SmsMaisDbContext db,
    ILogger<BackfillExecutanteService> logger) : IBackfillExecutanteService
{
    /// <summary>Tamanho do lote. A tabela tem ~21 mil linhas: carregar tudo de uma vez incharia o
    /// change tracker sem necessidade, e um lote pequeno demais multiplica ida e volta ao banco.
    /// <c>init</c> para os testes conseguirem exercitar a paginação sem semear 500 linhas.</summary>
    internal int TamanhoLote { get; init; } = 500;

    public async Task<BackfillExecutanteResultado> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var examinadas = 0;
        var preenchidas = 0;
        var semDado = 0;

        // Deslocamento das linhas que já foram examinadas e NÃO puderam ser preenchidas.
        //
        // É o detalhe que faz a paginação funcionar aqui: a consulta filtra por "executante ainda
        // nulo", então quem é preenchido sai do resultado sozinho e não desloca a janela — mas quem
        // não pôde ser preenchido continua no filtro e voltaria no próximo `Take`. Sem pular essas,
        // um lote inteiro sem executante ou repetiria para sempre, ou (se o laço parasse ali)
        // esconderia todas as linhas boas que vêm depois delas na ordem.
        var ignoradasAcumuladas = 0;

        while (true)
        {
            var lote = await db.Solicitacoes
                .Where(s => s.ProfissionalExecutanteCpf == null && s.RawSisreg != null)
                // Ordena pela PK (Guid v7 = ordenável por criação) para a janela ser estável entre
                // lotes; sem ordem definida o `Skip` poderia repetir ou pular linha.
                .OrderBy(s => s.Id)
                .Skip(ignoradasAcumuladas)
                .Take(TamanhoLote)
                .ToListAsync(cancellationToken);

            if (lote.Count == 0) break;

            var preenchidasNoLote = 0;

            foreach (var solicitacao in lote)
            {
                examinadas++;

                var marcacao = LerExecutante(solicitacao.RawSisreg!);
                if (marcacao is null)
                {
                    semDado++;
                    ignoradasAcumuladas++;
                    continue;
                }

                solicitacao.ProfissionalExecutanteCpf = marcacao.Value.Cpf;
                solicitacao.ProfissionalExecutanteNome = marcacao.Value.Nome;
                preenchidas++;
                preenchidasNoLote++;
            }

            if (preenchidasNoLote > 0) await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        var pendentes = await db.Solicitacoes
            .CountAsync(s => s.ProfissionalExecutanteCpf == null, cancellationToken);

        logger.LogInformation(
            "SISREG_BACKFILL_EXECUTANTE: {Examinadas} examinadas, {Preenchidas} preenchidas, "
            + "{SemDado} sem o dado no RAW, {Pendentes} ainda sem executante.",
            examinadas, preenchidas, semDado, pendentes);

        return new BackfillExecutanteResultado(
            examinadas, preenchidas, semDado, pendentes,
            preenchidas == 0
                ? "Nenhuma solicitação para completar — o executante já estava preenchido onde havia dado."
                : $"{preenchidas} solicitação(ões) ganharam o profissional executante. "
                  + $"{semDado} não tinham o dado na linha guardada; {pendentes} seguem sem executante "
                  + "(as que nunca tiveram linha crua do SISREG).");
    }

    /// <summary>
    /// Lê o executante de UMA linha crua, pelo próprio <see cref="AgendaTxtParser"/>.
    ///
    /// <para>Usa o parser em vez de um <c>Split(';')</c> local de propósito: ele é quem conhece o
    /// layout de 38 campos, a validação de forma e a limpeza de nulo. Um split aqui viraria uma
    /// segunda definição do formato, livre para divergir da primeira sem ninguém perceber.</para>
    /// </summary>
    private static (string Cpf, string? Nome)? LerExecutante(string raw)
    {
        // O RAW do caminho pontual é JSON e não carrega executante — descarta antes de gastar parser.
        if (raw.AsSpan().TrimStart().StartsWith("{")) return null;

        AgendaTxtParser.Resultado resultado;
        try
        {
            resultado = AgendaTxtParser.Parse(raw);
        }
        catch (Exception)
        {
            // Uma linha ruim não pode derrubar o backfill inteiro: ela só continua pendente.
            return null;
        }

        var marcacao = resultado.Marcacoes.FirstOrDefault();
        if (marcacao?.CpfProfissionalExecutante is not { Length: 11 } cpf) return null;

        return (cpf, marcacao.NomeProfissionalExecutante);
    }
}
