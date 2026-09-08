using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Ser;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>Quantos cadastros a pré-carga resolveu, e em quanto tempo.</summary>
public sealed record PreCargaCadastroDto(
    int Pedidos, int Resolvidos, int NaoEncontrados, int Falhas, int Sessoes, int DuracaoSegundos);

/// <summary>
/// Resolve MUITOS cadastros no SER de uma vez, em sessões paralelas, antes da importação começar.
///
/// <para><b>Por que fora do motor normal.</b> A sessão do SER é <i>stateful</i> — ViewState,
/// conversa Seam, aba aberta — e por isso o <see cref="ISerWebSessao"/> serializa tudo num
/// semáforo. Paralelizar dentro dele não daria ganho nenhum: as chamadas apenas se enfileirariam.
/// O que funciona é ter <b>N sessões independentes</b>, cada uma com seu cookie jar e seu login.
/// Medido contra o SER real em 28/08/2026: 4 sessões simultâneas com a MESMA credencial logaram
/// sem recusa, e cada uma devolveu o cadastro do CNS que pediu — nenhuma recebeu o paciente da
/// outra, que era o risco que mataria a ideia (velocidade que troca identidade de paciente é o
/// pior defeito possível aqui).</para>
///
/// <para><b>Por que ANTES e não durante.</b> A importação cria paciente, solicitação e exame no
/// mesmo <c>DbContext</c>, que não é seguro para uso concorrente. Então ela continua serial: o que
/// muda é encontrar o cadastro já resolvido no <see cref="CacheCadastroSer"/> em vez de esperar a
/// rede a cada linha.</para>
///
/// <para><b>Nunca lança por causa de um CNS.</b> O que não resolver aqui simplesmente não entra no
/// cache, e a importação faz o caminho de sempre para aquela linha — a pré-carga é otimização, não
/// pode virar um novo motivo de falha.</para>
/// </summary>
public interface IPreCargaCadastroSerService
{
    /// <param name="aoResolver">
    /// Chamado a cada cadastro resolvido, com (resolvidos, total). Existe porque esta fase pode
    /// levar minutos e <b>não escreve nada no banco</b>: sem este sinal a tela fica parada num
    /// número velho e o operador conclui que travou — foi o que quase fez cancelarem uma corrida
    /// saudável em 05/09/2026, depois de 7 minutos de silêncio. Nunca lança: reportar progresso não
    /// pode derrubar a pré-carga.
    /// </param>
    Task<PreCargaCadastroDto> ExecutarAsync(
        IReadOnlyCollection<string> cns, CancellationToken ct, Action<int, int>? aoResolver = null);
}

public sealed class PreCargaCadastroSerService(
    SmsMaisDbContext db,
    IServiceProvider provedor,
    CacheCadastroSer cache,
    Pacientes.Fhir.IPacienteFhirClient hub,
    ILogger<PreCargaCadastroSerService> logger) : IPreCargaCadastroSerService
{
    /// <summary>
    /// Consultas simultâneas ao hub na triagem. Mais alto que o teto do SER de propósito: aqui é a
    /// nossa própria API, na mesma máquina, sem sessão nem orçamento anti-robô a proteger.
    /// </summary>
    private const int TetoConsultasHub = 16;

    /// <summary>
    /// Teto de sessões simultâneas, independente do que a configuração pedir.
    ///
    /// <para>O SER é sistema público do Estado e o ganho satura rápido: o gargalo deixa de ser a
    /// rede e passa a ser a nossa própria escrita no banco. Abrir dezenas de sessões só somaria
    /// carga no alvo sem devolver tempo — e chamaria atenção para a integração.</para>
    /// </summary>
    private const int TetoSessoes = 8;

    public async Task<PreCargaCadastroDto> ExecutarAsync(
        IReadOnlyCollection<string> cns, CancellationToken ct, Action<int, int>? aoResolver = null)
    {
        var relogio = System.Diagnostics.Stopwatch.StartNew();

        var alvos = cns
            .Select(c => new string([.. (c ?? string.Empty).Where(char.IsDigit)]))
            .Where(c => c.Length == 15)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var config = await db.SisregConfiguracoes.AsNoTracking()
            .Select(c => new { c.FonteCadastroPaciente, c.ConsultasSimultaneasSer })
            .FirstOrDefaultAsync(ct);

        // Só faz sentido com o SER na jogada: pelo SISREG cada consulta gasta o orçamento
        // anti-robô, e paralelizar lá é a receita para o CAPTCHA.
        var peloSer = config?.FonteCadastroPaciente is FonteCadastroPaciente.Ser
                      or FonteCadastroPaciente.SerComFallbackSisreg;
        var sessoes = Math.Clamp(config?.ConsultasSimultaneasSer ?? 1, 1, TetoSessoes);

        if (!peloSer || sessoes <= 1 || alvos.Count == 0)
        {
            return new PreCargaCadastroDto(alvos.Count, 0, 0, 0, sessoes, 0);
        }

        // TRIAGEM: quem já é nosso não precisa do SER.
        //
        // O importador só vai ao CADSUS quando não acha o paciente pelo CNS — para todo o resto ele
        // reusa o cadastro e sequer olha o cache que esta pré-carga encheu. Perguntar mesmo assim
        // custava caro: medido no CDT em 08/09/2026, 5.717 consultas ao SER numa janela cujos 5.604
        // agendamentos JÁ tinham paciente resolvido (zero sem paciente). São ~35 minutos de espera
        // antes de importar a primeira linha — todo dia, em toda unidade — e carga inútil num
        // sistema público do Estado.
        //
        // Pergunta ao HUB pela API (ADR-0010), não à tabela `fhir.patient`. E errar aqui é barato
        // nos dois sentidos: dizer "já conheço" quem não conhecemos apenas faz o importador
        // perguntar na hora, e dizer "não conheço" quem já existe recai no comportamento antigo.
        var totalLido = alvos.Count;
        alvos = await TriarDesconhecidosAsync(alvos, ct);

        logger.LogInformation(
            "SER/pré-carga: {Total} CNS no arquivo, {Novos} desconhecidos — {Poupadas} consulta(s) "
            + "ao SER poupadas por já termos o paciente.",
            totalLido, alvos.Count, totalLido - alvos.Count);

        if (alvos.Count == 0) return new PreCargaCadastroDto(0, 0, 0, 0, sessoes, 0);

        var falhas = 0;
        var processados = 0;
        var fatias = Particionar(alvos, sessoes);

        // Reporta a cada 25 para não transformar 5 mil resoluções em 5 mil escritas de progresso —
        // o remédio da tela parada não pode virar carga no banco.
        void Reportar()
        {
            var feitos = Interlocked.Increment(ref processados);
            if (aoResolver is null || (feitos % 25 != 0 && feitos != alvos.Count)) return;
            try { aoResolver(feitos, alvos.Count); }
            catch (Exception ex) { logger.LogDebug(ex, "SER/pré-carga: falha ao reportar progresso."); }
        }

        logger.LogInformation(
            "SER/pré-carga: {Qtd} CNS em {Sessoes} sessões simultâneas.", alvos.Count, fatias.Count);

        await Task.WhenAll(fatias.Select(async fatia =>
        {
            // Uma instância PRÓPRIA do motor por worker: cada uma tem seu cookie jar, seu login e
            // seu ViewState. Compartilhar a instância singleton apenas os faria disputar o mesmo
            // semáforo — e, pior, embaralhar o estado da aba entre eles.
            var sessao = ActivatorUtilities.CreateInstance<SerWebSessao>(provedor);
            try
            {
                var motor = ActivatorUtilities.CreateInstance<SerNovaSolicitacaoService>(provedor, sessao);
                var cadastro = new SerCadastroPacienteService(motor);

                foreach (var alvo in fatia)
                {
                    if (ct.IsCancellationRequested) return;

                    try
                    {
                        cache.Guardar(alvo, await cadastro.ConsultarPorCnsAsync(alvo, ct));
                        Reportar();
                    }
                    catch (NaoEncontradoException)
                    {
                        // A fonte respondeu: esse cidadão não está no CADSUS. Guardar o "não" evita
                        // perguntar de novo na importação.
                        cache.GuardarNaoEncontrado(alvo);
                        Reportar();
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref falhas);
                        logger.LogDebug(ex, "SER/pré-carga: falhou para um CNS; segue o baile.");
                        Reportar();
                    }
                }
            }
            finally
            {
                (sessao as IDisposable)?.Dispose();
            }
        }));

        relogio.Stop();

        var resultado = new PreCargaCadastroDto(
            alvos.Count, cache.Resolvidos, cache.NaoEncontrados, falhas, fatias.Count,
            (int)relogio.Elapsed.TotalSeconds);

        logger.LogInformation(
            "SER/pré-carga: {Resolvidos} resolvidos, {NaoEncontrados} sem cadastro, {Falhas} falhas "
            + "em {Seg}s com {Sessoes} sessões.",
            resultado.Resolvidos, resultado.NaoEncontrados, resultado.Falhas,
            resultado.DuracaoSegundos, resultado.Sessoes);

        return resultado;
    }

    /// <summary>
    /// Dos CNS do arquivo, quais o hub <b>ainda não conhece</b> — os únicos que valem uma ida ao SER.
    ///
    /// <para>Usa exatamente a mesma pergunta que o importador faz antes de decidir se consulta o
    /// CADSUS, para não haver duas réguas de "já temos este paciente".</para>
    ///
    /// <para><b>Hub mudo conta como desconhecido.</b> Tratar indisponibilidade como "já conheço"
    /// esconderia paciente novo e faria a importação parar nele; na dúvida, pré-carrega — que é o
    /// comportamento anterior a esta triagem.</para>
    /// </summary>
    private async Task<List<string>> TriarDesconhecidosAsync(
        List<string> alvos, CancellationToken ct)
    {
        var desconhecidos = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            alvos,
            new ParallelOptions { MaxDegreeOfParallelism = TetoConsultasHub, CancellationToken = ct },
            async (cns, token) =>
            {
                try
                {
                    // COM o system — ver a mesma nota em PacientesService.ObterPorCnsAsync. Sem ele
                    // o hub lê o CNS como CPF e responde "não conheço" para TODO mundo: a triagem
                    // virava um no-op caro, mandando 100% dos CNS ao SER (5.786 numa varredura só,
                    // medido no CDT em 08/09/2026) exatamente o que ela existia para evitar.
                    var bundle = await hub.BuscarAsync(
                        identifier: $"{Pacientes.Fhir.PatientMergeFhir.SystemCns}|{cns}", ct: token);
                    var achou = bundle.Entry
                        .Select(e => e.Resource)
                        .OfType<Hl7.Fhir.Model.Patient>()
                        .Any();

                    if (!achou) desconhecidos.Add(cns);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex, "SER/pré-carga: hub não respondeu para um CNS; tratando como desconhecido.");
                    desconhecidos.Add(cns);
                }
            });

        return [.. desconhecidos];
    }

    /// <summary>
    /// Reparte os CNS entre os workers de forma intercalada (round-robin), não em blocos: os
    /// primeiros CNS de um export tendem a ser da mesma agenda e do mesmo dia, e um bloco contíguo
    /// poderia cair todo em pacientes já conhecidos enquanto outro worker fica com os caros.
    /// </summary>
    private static List<List<string>> Particionar(List<string> alvos, int partes)
    {
        var quantas = Math.Min(partes, alvos.Count);
        var fatias = Enumerable.Range(0, quantas).Select(_ => new List<string>()).ToList();
        for (var i = 0; i < alvos.Count; i++) fatias[i % quantas].Add(alvos[i]);
        return fatias;
    }
}
