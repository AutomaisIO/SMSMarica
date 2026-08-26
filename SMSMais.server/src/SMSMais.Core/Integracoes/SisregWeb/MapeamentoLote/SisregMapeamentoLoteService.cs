using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.SisregWeb.Mapeamento;
using SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background;
using SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote;

/// <summary>
/// Sincroniza o mapeamento (profissionais + procedimentos, e o vínculo FHIR) de <b>todas</b> as
/// unidades configuradas de uma vez — o "SISREG Sincroniza tudo" do #118.
///
/// <para><b>Sequencial, nunca em paralelo:</b> todas as unidades saem para o SISREG pelo mesmo IP
/// (túnel WireGuard, <c>docs/sisreg-egress.md</c>) e dividem o mesmo orçamento anti-robô que a
/// varredura de agenda. O lote roda unidade a unidade e espaça as requisições para caber em
/// <see cref="SisregMapeamentoLoteOpcoes.RequisicoesPorHora"/> (500/h por padrão, pedido do
/// operador). Um lote por vez em toda a instalação, e não inicia enquanto houver varredura ou
/// importação vivas.</para>
///
/// <para><b>Uma unidade ruim não derruba o lote:</b> falha isolada vira erro contabilizado e segue.
/// Só o CAPTCHA para tudo — insistir depois dele apenas aprofunda o bloqueio.</para>
/// </summary>
public interface ISisregMapeamentoLoteService
{
    /// <summary>Dispara o lote AGORA (botão manual). Enfileira e devolve 202.</summary>
    Task<MapeamentoLoteAceitoDto> IniciarAsync(CancellationToken cancellationToken = default);

    /// <summary>Disparo pelo scheduler. Devolve false em colisão/sem unidades (não é erro).</summary>
    Task<bool> DispararAgendadoAsync(CancellationToken cancellationToken = default);

    /// <summary>Executa o lote — chamado pelo runner, fora de qualquer request.</summary>
    Task ExecutarAsync(MapeamentoLoteJob job, CancellationToken cancellationToken = default);

    MapeamentoLoteStatusDto? ObterStatus();

    bool Cancelar();

    Task<MapeamentoLoteAgendamentoDto> ObterAgendamentoAsync(CancellationToken cancellationToken = default);

    Task<MapeamentoLoteAgendamentoDto> SalvarAgendamentoAsync(
        SalvarMapeamentoLoteAgendamentoRequest request, CancellationToken cancellationToken = default);
}

public sealed class SisregMapeamentoLoteService(
    SmsMaisDbContext db,
    IServiceScopeFactory scopeFactory,
    IIntegracaoCredencialService credenciais,
    IUsuarioAtualAccessor usuarioAtual,
    IMapeamentoLoteFila fila,
    MapeamentoLoteEstadoVivo estadoVivo,
    Varredura.Background.VarreduraSisregEstadoVivo varreduraEstadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    IOptions<SisregMapeamentoLoteOpcoes> opcoes,
    ILogger<SisregMapeamentoLoteService> logger) : ISisregMapeamentoLoteService
{
    public const string ChaveAtivo = "mapeamentoLoteAtivo";
    public const string ChaveHora = "mapeamentoLoteHoraLocal";
    public const string HoraPadrao = "03:30";

    private readonly SisregMapeamentoLoteOpcoes _opcoes = opcoes.Value;

    // ------------------------------------------------------------------ disparo

    public async Task<MapeamentoLoteAceitoDto> IniciarAsync(CancellationToken cancellationToken = default)
    {
        var aceito = await IniciarNucleoAsync(
            DisparoSincronizacao.Manual, usuarioAtual.UsuarioId, lancar: true, cancellationToken);
        return aceito!;
    }

    public async Task<bool> DispararAgendadoAsync(CancellationToken cancellationToken = default)
    {
        var aceito = await IniciarNucleoAsync(
            DisparoSincronizacao.Agendado, usuarioId: null, lancar: false, cancellationToken);
        return aceito is not null;
    }

    private async Task<MapeamentoLoteAceitoDto?> IniciarNucleoAsync(
        DisparoSincronizacao disparo, Guid? usuarioId, bool lancar, CancellationToken ct)
    {
        // O humano sempre ganha do robô, e o SISREG é uma saída só: não inicia sobre nenhum
        // trabalho vivo que também consuma o orçamento anti-robô.
        if (estadoVivo.EmExecucao
            || varreduraEstadoVivo.ObterAtual() is not null
            || importacaoEstadoVivo.ObterAtual() is not null)
        {
            if (lancar)
            {
                throw new ConflitoException(
                    "sisreg.lote_em_andamento",
                    "Já há um trabalho do SISREG em andamento (sincronização, varredura ou "
                    + "importação). Como todos falam com o SISREG pela mesma saída, espere terminar.");
            }
            return null;
        }

        var unidades = await CarregarUnidadesConfiguradasAsync(ct);
        if (unidades.Count == 0)
        {
            if (lancar)
            {
                throw new ValidacaoException(
                    "sisreg.lote_sem_unidades",
                    "Nenhuma unidade com CNES e mapeamento do SISREG já iniciado. Faça o primeiro "
                    + "mapeamento manual de cada unidade nova antes de sincronizar tudo.");
            }
            return null;
        }

        if (!fila.TentarEnfileirar(new MapeamentoLoteJob(disparo, usuarioId)))
        {
            if (lancar)
            {
                throw new ConflitoException(
                    "sisreg.lote_fila_cheia", "Já há uma sincronização na fila. Tente de novo em instantes.");
            }
            return null;
        }

        return new MapeamentoLoteAceitoDto(
            unidades.Count,
            $"Sincronização enfileirada: {unidades.Count} unidade(s). O mapeamento e o vínculo FHIR "
            + "de cada uma serão atualizados em sequência, respeitando o limite de requisições do SISREG.");
    }

    // ------------------------------------------------------------------ execução

    public async Task ExecutarAsync(MapeamentoLoteJob job, CancellationToken cancellationToken = default)
    {
        var unidades = await CarregarUnidadesConfiguradasAsync(cancellationToken);
        if (unidades.Count == 0) return;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

        var progresso = new ProgressoMapeamentoLote
        {
            Disparo = job.Disparo,
            UnidadesTotal = unidades.Count,
            IniciadoEm = DateTime.UtcNow,
        };
        estadoVivo.Iniciar(progresso, cts);

        var pacer = new PacerRequisicoes(_opcoes.IntervaloMinimoRequisicao);
        Func<CancellationToken, Task> antesDeCadaRequisicao = async c =>
        {
            await pacer.EsperarAsync(c);
            Interlocked.Increment(ref progresso.RequisicoesFeitas);
        };

        try
        {
            foreach (var unidade in unidades)
            {
                token.ThrowIfCancellationRequested();
                progresso.UnidadeAtual = unidade.Nome;

                try
                {
                    // Escopo próprio por unidade: DbContext limpo, sem acumular rastreamento da rede
                    // inteira num contexto só.
                    using var scope = scopeFactory.CreateScope();
                    var mapeamento = scope.ServiceProvider.GetRequiredService<ISisregMapeamentoService>();

                    var atualizado = await mapeamento.AtualizarNoContextoAsync(
                        unidade, job.UsuarioId, antesDeCadaRequisicao, token);
                    Interlocked.Add(ref progresso.ProfissionaisEncontrados, atualizado.ProfissionaisEncontrados);
                    Interlocked.Add(ref progresso.ProfissionaisNovos, atualizado.ProfissionaisNovos);

                    // Passo FHIR: cria/vincula os habilitados no hub. Fala com o hub, não com o
                    // SISREG — não consome o orçamento anti-robô.
                    var fhir = await mapeamento.SincronizarFhirNoContextoAsync(unidade, job.UsuarioId, token);
                    Interlocked.Add(ref progresso.PractitionersCriados, fhir.Criados);
                    Interlocked.Add(ref progresso.PractitionersVinculados, fhir.Vinculados);
                }
                catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
                {
                    // CAPTCHA para o lote inteiro: relogar não resolve e insistir só aprofunda o
                    // bloqueio. Um humano precisa abrir o SISREG no navegador e resolver.
                    progresso.UltimoErro =
                        "O SISREG passou a exigir CAPTCHA (proteção anti-robô por volume) e a "
                        + "sincronização parou. O que já entrou está salvo. Resolva o CAPTCHA no "
                        + "navegador e rode de novo.";
                    Interlocked.Increment(ref progresso.UnidadesComErro);
                    logger.LogError(
                        "SISREG_MAPEAMENTO_LOTE_CAPTCHA: parou em {Unidade} após {Req} requisições, "
                        + "{Feitas}/{Total} unidades.",
                        unidade.Nome, progresso.RequisicoesFeitas, progresso.UnidadesFeitas, progresso.UnidadesTotal);
                    Interlocked.Increment(ref progresso.UnidadesFeitas);
                    break;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref progresso.UnidadesFeitas);
                    throw;
                }
                catch (Exception ex)
                {
                    // Uma unidade ruim não aborta a rede toda: contabiliza e segue.
                    Interlocked.Increment(ref progresso.UnidadesComErro);
                    progresso.UltimoErro = $"{unidade.Nome}: {ex.Message}";
                    logger.LogWarning(ex, "SISREG_MAPEAMENTO_LOTE: falha na unidade {Unidade}.", unidade.Nome);
                    Interlocked.Increment(ref progresso.UnidadesFeitas);
                    continue;
                }

                Interlocked.Increment(ref progresso.UnidadesFeitas);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "SISREG_MAPEAMENTO_LOTE_CANCELADO: {Feitas}/{Total} unidades feitas.",
                progresso.UnidadesFeitas, progresso.UnidadesTotal);
        }
        finally
        {
            estadoVivo.Finalizar();
            logger.LogInformation(
                "SISREG_MAPEAMENTO_LOTE_FIM: {Feitas}/{Total} unidades, {Req} requisições, "
                + "{Novos} profissionais novos, {Criados} practitioners criados, {Vinc} vinculados, {Erros} com erro.",
                progresso.UnidadesFeitas, progresso.UnidadesTotal, progresso.RequisicoesFeitas,
                progresso.ProfissionaisNovos, progresso.PractitionersCriados,
                progresso.PractitionersVinculados, progresso.UnidadesComErro);
        }
    }

    public MapeamentoLoteStatusDto? ObterStatus() => estadoVivo.ObterAtual();

    public bool Cancelar() => estadoVivo.Cancelar();

    // ------------------------------------------------------------------ agendamento (config)

    public async Task<MapeamentoLoteAgendamentoDto> ObterAgendamentoAsync(CancellationToken cancellationToken = default)
    {
        var json = await LerParametrosAsync(cancellationToken);
        return new MapeamentoLoteAgendamentoDto(
            json?[ChaveAtivo]?.GetValue<bool>() ?? false,
            json?[ChaveHora]?.GetValue<string>() ?? HoraPadrao);
    }

    public async Task<MapeamentoLoteAgendamentoDto> SalvarAgendamentoAsync(
        SalvarMapeamentoLoteAgendamentoRequest request, CancellationToken cancellationToken = default)
    {
        var hora = Normalizar(request.HoraLocal);

        // Merge: o baseUrl/autoLogin do SISREG vivem no MESMO ParametrosJson — sobrescrever o JSON
        // inteiro apagaria a credencial de acesso.
        var atual = await credenciais.ObterAsync(SisregWebSessao.Provedor, cancellationToken);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[ChaveAtivo] = request.Ativo;
        json[ChaveHora] = hora;

        await credenciais.AtualizarAsync(
            SisregWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            cancellationToken);

        return new MapeamentoLoteAgendamentoDto(request.Ativo, hora);
    }

    // ------------------------------------------------------------------ interno

    /// <summary>
    /// Unidades elegíveis ao lote: têm CNES e já possuem mapeamento do SISREG iniciado (linhas em
    /// <c>sisreg_profissional_unidade</c>). Unidade nova entra depois do primeiro mapeamento manual.
    /// </summary>
    private async Task<List<Unidade>> CarregarUnidadesConfiguradasAsync(CancellationToken ct)
    {
        var unidadeIds = await db.SisregProfissionaisUnidade
            .Select(p => p.UnidadeId)
            .Distinct()
            .ToListAsync(ct);

        if (unidadeIds.Count == 0) return [];

        return await db.Unidades.AsNoTracking()
            .Where(u => unidadeIds.Contains(u.Id) && u.Cnes != null && u.Cnes != "")
            .OrderBy(u => u.Nome)
            .ToListAsync(ct);
    }

    private async Task<JsonObject?> LerParametrosAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(SisregWebSessao.Provedor, cancellationToken);
            return Parse(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            return null;
        }
    }

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string Normalizar(string? hora)
    {
        if (!TimeOnly.TryParseExact(hora?.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var t))
        {
            throw new ValidacaoException(
                "sisreg.hora_invalida",
                $"Hora inválida: \"{hora}\". Use o formato HH:mm (ex.: 03:30).");
        }

        return t.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Espaça as requisições ao SISREG para caber no teto por hora. Serializa com um semáforo: o
    /// lote é sequencial, mas o gancho pode ser chamado de dentro do mesmo fluxo sem corrida.
    /// </summary>
    private sealed class PacerRequisicoes(TimeSpan intervalo)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private DateTime _proximo = DateTime.UtcNow;

        public async Task EsperarAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var agora = DateTime.UtcNow;
                if (_proximo > agora) await Task.Delay(_proximo - agora, ct);
                var baseTempo = DateTime.UtcNow > _proximo ? DateTime.UtcNow : _proximo;
                _proximo = baseTempo + intervalo;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
