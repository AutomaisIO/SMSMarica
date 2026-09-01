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
using SMSMais.Core.Integracoes.SisregWeb.Unidades;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote;

/// <summary>
/// Sincroniza a rede inteira do SISREG: <b>descobre</b> as unidades que a credencial enxerga, cria
/// aqui as que faltam e reconcilia o mapeamento (profissionais + procedimentos + vínculo FHIR) de
/// cada uma — o "SISREG Sincroniza tudo" do #118.
///
/// <para><b>Antes olhava só para dentro.</b> A versão original elegia as unidades por
/// "já tem linha em <c>sisreg_profissional_unidade</c>", ou seja, as que alguém já havia mapeado à
/// mão: em Maricá isso eram 6 de 43. Perguntar ao SISREG quais unidades existem custa <b>uma</b>
/// requisição (<see cref="ISisregCatalogoUnidadesService"/>), então a descoberta virou o primeiro
/// passo do lote.</para>
///
/// <para><b>Por que não mapeia tudo toda vez.</b> Mapear uma unidade custa 1 requisição pela lista
/// de profissionais mais 1 por profissional pelos procedimentos. A rede toda daria da ordem de
/// 2.400 requisições — mais de três vezes o teto em que o CAPTCHA aparece (~700 por operador).
/// Então o lote trata as unidades <b>da mais antiga para a mais nova</b> e pula as que ainda estão
/// dentro do TTL: cada rodada cabe no orçamento e, em algumas rodadas, a rede inteira se cobre.
/// Como a ordem é por idade, quem é pulado sobe na fila sozinho — não precisa de cursor.</para>
///
/// <para><b>Sequencial, nunca em paralelo:</b> todas as unidades saem pelo mesmo IP (túnel
/// WireGuard, <c>docs/sisreg-egress.md</c>) e dividem o orçamento anti-robô com a varredura de
/// agenda e a importação. Um lote por vez em toda a instalação, e não inicia enquanto houver
/// varredura ou importação vivas.</para>
///
/// <para><b>Uma unidade ruim não derruba o lote:</b> falha isolada vira erro contabilizado no item
/// daquela unidade e segue. Só o CAPTCHA para tudo — insistir depois dele apenas aprofunda o
/// bloqueio.</para>
/// </summary>
public interface ISisregMapeamentoLoteService
{
    /// <summary>Dispara o lote AGORA (botão manual). Enfileira e devolve 202.</summary>
    Task<MapeamentoLoteAceitoDto> IniciarAsync(CancellationToken cancellationToken = default);

    /// <summary>Disparo pelo scheduler. Devolve false em colisão (não é erro).</summary>
    Task<bool> DispararAgendadoAsync(CancellationToken cancellationToken = default);

    /// <summary>Executa o lote — chamado pelo runner, fora de qualquer request.</summary>
    Task ExecutarAsync(MapeamentoLoteJob job, CancellationToken cancellationToken = default);

    MapeamentoLoteStatusDto? ObterStatus();

    bool Cancelar();

    Task<MapeamentoLoteAgendamentoDto> ObterAgendamentoAsync(CancellationToken cancellationToken = default);

    Task<MapeamentoLoteAgendamentoDto> SalvarAgendamentoAsync(
        SalvarMapeamentoLoteAgendamentoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sincronizações recentes, mais nova primeiro.</summary>
    Task<IReadOnlyList<MapeamentoLoteExecucaoDto>> ListarExecucoesAsync(
        int limite, CancellationToken cancellationToken = default);

    /// <summary>Detalhe por unidade de uma execução.</summary>
    Task<IReadOnlyList<MapeamentoLoteExecucaoItemDto>> ListarItensAsync(
        Guid execucaoId, CancellationToken cancellationToken = default);
}

public sealed class SisregMapeamentoLoteService(
    SmsMaisDbContext db,
    IServiceScopeFactory scopeFactory,
    IIntegracaoCredencialService credenciais,
    IUsuarioAtualAccessor usuarioAtual,
    IMapeamentoLoteFila fila,
    MapeamentoLoteEstadoVivo estadoVivo,
    SisregOrcamentoRequisicoes orcamento,
    Varredura.Background.VarreduraSisregEstadoVivo varreduraEstadoVivo,
    Importacao.Background.SisregImportacaoEstadoVivo importacaoEstadoVivo,
    IOptions<SisregMapeamentoLoteOpcoes> opcoes,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    ILogger<SisregMapeamentoLoteService> logger) : ISisregMapeamentoLoteService
{
    public const string ChaveAtivo = "mapeamentoLoteAtivo";
    public const string ChaveHora = "mapeamentoLoteHoraLocal";
    public const string HoraPadrao = "03:30";

    private readonly SisregMapeamentoLoteOpcoes _opcoes = opcoes.Value;
    private readonly SisregOrcamentoOpcoes _orcamentoOpcoes = orcamentoOpcoes.Value;

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

        // Sem orçamento não adianta enfileirar: a primeira requisição já bateria no CAPTCHA. E com
        // orçamento de sobra insuficiente também não — começar para pular tudo só gasta a
        // requisição da descoberta e polui o histórico com uma rodada de zero.
        var restante = orcamento.Restante(_orcamentoOpcoes.TetoAutomatico);
        if (restante < _opcoes.OrcamentoMinimoParaIniciar)
        {
            if (lancar)
            {
                var espera = orcamento.EsperaAteLiberar();
                throw new ConflitoException(
                    "sisreg.orcamento_esgotado",
                    $"Restam só {restante} acessos ao SISREG nesta hora (o teto é "
                    + $"{_orcamentoOpcoes.TetoAutomatico}) — não dá para sincronizar nem uma unidade. "
                    + "Insistir agora é o que faz aparecer o CAPTCHA, que bloqueia o operador por 24h."
                    + (espera is { } e ? $" Tente de novo em {Math.Ceiling(e.TotalMinutes)} min." : string.Empty));
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

        // Denominador provisório: quantas unidades já temos. A conta boa só existe depois da
        // descoberta, que é o primeiro passo da execução.
        var candidatas = await db.Unidades.CountAsync(u => u.Ativo && u.Cnes != null && u.Cnes != "", ct);

        return new MapeamentoLoteAceitoDto(
            candidatas,
            "Sincronização enfileirada. Primeiro o sistema lê no SISREG a lista de unidades e cria "
            + "as que faltam aqui; depois atualiza o mapeamento de cada uma, da mais desatualizada "
            + $"para a mais recente, dentro do limite de {restante} requisições disponíveis nesta hora.");
    }

    // ------------------------------------------------------------------ execução

    public async Task ExecutarAsync(MapeamentoLoteJob job, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

        var agora = DateTime.UtcNow;

        await FecharOrfasAsync(agora, token);

        var execucao = new SisregMapeamentoLoteExecucao
        {
            Id = Guid.CreateVersion7(),
            Disparo = job.Disparo,
            Status = StatusVarredura.EmExecucao,
            IniciadoEm = agora,
            CriadoPor = job.UsuarioId,
        };
        db.SisregMapeamentoLoteExecucoes.Add(execucao);
        await db.SaveChangesAsync(token);

        var progresso = new ProgressoMapeamentoLote
        {
            Disparo = job.Disparo,
            IniciadoEm = agora,
            ExecucaoId = execucao.Id,
            OrcamentoRestante = orcamento.Restante(_orcamentoOpcoes.TetoAutomatico),
        };
        estadoVivo.Iniciar(progresso, cts);

        var pacer = new PacerRequisicoes(_opcoes.IntervaloMinimoRequisicao);
        Func<CancellationToken, Task> antesDeCadaRequisicao = async c =>
        {
            await pacer.EsperarAsync(c);
            Interlocked.Increment(ref progresso.RequisicoesFeitas);
        };

        var status = StatusVarredura.Concluida;

        try
        {
            // ---- passo 1: descoberta (1 requisição) ----
            var descoberta = await DescobrirUnidadesAsync(execucao, progresso, antesDeCadaRequisicao, token);

            // ---- passo 2: mapeamento, da mais antiga para a mais nova ----
            progresso.Fase = ProgressoMapeamentoLote.FaseMapeamento;
            status = await MapearUnidadesAsync(
                execucao, progresso, descoberta.CriadasIds, descoberta.CnesNoSisreg,
                job.UsuarioId, antesDeCadaRequisicao, token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            status = StatusVarredura.Cancelada;
            logger.LogInformation(
                "SISREG_MAPEAMENTO_LOTE_CANCELADO: {Feitas}/{Total} unidades feitas.",
                progresso.UnidadesFeitas, progresso.UnidadesTotal);
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            // CAPTCHA logo na descoberta: o mapeamento nem chegou a começar. É "parcial" e não
            // "erro" pelo mesmo motivo da varredura — dizer "erro" sugere defeito nosso, quando o
            // que houve foi o SISREG fechando a porta por volume.
            status = StatusVarredura.Parcial;
            progresso.UltimoErro =
                "O SISREG passou a exigir CAPTCHA (proteção anti-robô por volume) antes mesmo de "
                + "listar as unidades. Resolva o CAPTCHA no navegador com esse operador e rode de novo.";
            execucao.MensagemErro = progresso.UltimoErro;
            logger.LogError("SISREG_MAPEAMENTO_LOTE_CAPTCHA: bloqueado na descoberta das unidades.");
        }
        catch (Exception ex)
        {
            status = StatusVarredura.Erro;
            progresso.UltimoErro = ex.Message;
            logger.LogError(ex, "SISREG_MAPEAMENTO_LOTE: falha geral.");
        }
        finally
        {
            estadoVivo.Finalizar();
            await FinalizarAsync(execucao, status, progresso, CancellationToken.None);

            logger.LogInformation(
                "SISREG_MAPEAMENTO_LOTE_FIM ({Status}): {NoSisreg} unidades no SISREG, {Criadas} criadas, "
                + "{Mapeadas} mapeadas, {Puladas} puladas, {Erros} com erro, {Req} requisições, "
                + "{ProfNovos} profissionais novos, {ProcNovos} procedimentos novos.",
                status, progresso.UnidadesNoSisreg, progresso.UnidadesCriadas, progresso.UnidadesMapeadas,
                progresso.UnidadesPuladas, progresso.UnidadesComErro, progresso.RequisicoesFeitas,
                progresso.ProfissionaisNovos, progresso.ProcedimentosNovos);
        }
    }

    /// <summary>
    /// Passo 1: pergunta ao SISREG quais unidades existem e cria as que faltam. Falhar aqui
    /// <b>não</b> aborta o lote — ficar sem descobrir unidade nova é ruim, mas deixar de atualizar
    /// as que já existem seria pior.
    /// </summary>
    /// <summary>O que a descoberta entrega ao passo de mapeamento.</summary>
    /// <param name="CriadasIds">Unidades que nasceram nesta execução.</param>
    /// <param name="CnesNoSisreg">Recorte "de lá para cá"; vazio = descoberta falhou, vale tudo.</param>
    private sealed record Descoberta(IReadOnlySet<Guid> CriadasIds, IReadOnlySet<string> CnesNoSisreg);

    private async Task<Descoberta> DescobrirUnidadesAsync(
        SisregMapeamentoLoteExecucao execucao,
        ProgressoMapeamentoLote progresso,
        Func<CancellationToken, Task> antesDeCadaRequisicao,
        CancellationToken token)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var catalogo = scope.ServiceProvider.GetRequiredService<ISisregCatalogoUnidadesService>();

            await antesDeCadaRequisicao(token);
            var reconciliacao = await catalogo.ReconciliarAsync(token);

            progresso.UnidadesNoSisreg = reconciliacao.NoSisreg;
            progresso.UnidadesCriadas = reconciliacao.Criadas;
            execucao.UnidadesNoSisreg = reconciliacao.NoSisreg;
            execucao.UnidadesCriadas = reconciliacao.Criadas;
            execucao.UnidadesComCnesPreenchido = reconciliacao.CnesPreenchido;
            await db.SaveChangesAsync(token);

            return new Descoberta(reconciliacao.CriadasIds, reconciliacao.CnesNoSisreg);
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            throw; // CAPTCHA para tudo — tratado lá em cima
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            progresso.UltimoErro =
                "Não foi possível ler a lista de unidades do SISREG — a sincronização seguiu só com "
                + $"as unidades já cadastradas aqui. Motivo: {ex.Message}";
            execucao.MensagemErro = progresso.UltimoErro;
            logger.LogWarning(ex, "SISREG_MAPEAMENTO_LOTE: descoberta de unidades falhou; seguindo com o cadastro local.");
            // Sem recorte: não dá para saber o que existe lá, então vale o cadastro inteiro.
            return new Descoberta(new HashSet<Guid>(), new HashSet<string>(StringComparer.Ordinal));
        }
    }

    /// <summary>Passo 2: reconcilia o mapeamento das unidades elegíveis, respeitando TTL e orçamento.</summary>
    private async Task<StatusVarredura> MapearUnidadesAsync(
        SisregMapeamentoLoteExecucao execucao,
        ProgressoMapeamentoLote progresso,
        IReadOnlySet<Guid> criadasAgora,
        IReadOnlySet<string> cnesNoSisreg,
        Guid? usuarioId,
        Func<CancellationToken, Task> antesDeCadaRequisicao,
        CancellationToken token)
    {
        var candidatas = await CarregarCandidatasAsync(cnesNoSisreg, token);
        progresso.UnidadesTotal = candidatas.Count;
        execucao.UnidadesTotal = candidatas.Count;
        await db.SaveChangesAsync(token);

        var status = StatusVarredura.Concluida;

        foreach (var candidata in candidatas)
        {
            token.ThrowIfCancellationRequested();

            var restante = orcamento.Restante(_orcamentoOpcoes.TetoAutomatico);
            progresso.OrcamentoRestante = restante;

            // TTL: pular é o comportamento NORMAL e desejado, não uma falha. É o que permite o
            // botão apontar para a rede inteira sem estourar o orçamento.
            if (!candidata.PrecisaMapear)
            {
                await RegistrarItemAsync(
                    execucao, candidata, ResultadoUnidadeLote.PuladaPorTtl, criadasAgora,
                    observacao: $"Mapeamento atualizado em {candidata.MapeadoEm:dd/MM/yyyy} — dentro do "
                        + $"limite de {candidata.TtlDias} dias desta unidade.",
                    token: token);
                Interlocked.Increment(ref progresso.UnidadesPuladas);
                Interlocked.Increment(ref progresso.UnidadesFeitas);
                continue;
            }

            // Nunca entramos numa unidade que não cabe: o mapeamento só é gravado ao fim, então
            // parar no meio jogaria fora tudo que já foi gasto com ela. Como a ordem é por idade,
            // a unidade preterida vira a mais antiga e entra primeiro na próxima rodada.
            if (candidata.CustoEstimado > restante)
            {
                await RegistrarItemAsync(
                    execucao, candidata, ResultadoUnidadeLote.PuladaPorOrcamento, criadasAgora,
                    observacao: $"Precisa de ~{candidata.CustoEstimado} requisições e só restam {restante} "
                        + "nesta hora. Entra na próxima sincronização, na frente da fila.",
                    token: token);
                Interlocked.Increment(ref progresso.UnidadesPuladas);
                Interlocked.Increment(ref progresso.UnidadesFeitas);
                status = StatusVarredura.Parcial;
                continue;
            }

            progresso.UnidadeAtual = candidata.Nome;
            var requisicoesAntes = progresso.RequisicoesFeitas;

            try
            {
                // Escopo próprio por unidade: DbContext limpo, sem acumular rastreamento da rede
                // inteira num contexto só.
                using var scope = scopeFactory.CreateScope();
                var mapeamento = scope.ServiceProvider.GetRequiredService<ISisregMapeamentoService>();

                var atualizado = await mapeamento.AtualizarNoContextoAsync(
                    candidata.Unidade, usuarioId, antesDeCadaRequisicao, token, _opcoes.TtlProcedimentos);

                // Passo FHIR: cria/vincula os habilitados no hub. Fala com o hub, não com o
                // SISREG — não consome o orçamento anti-robô.
                var fhir = await mapeamento.SincronizarFhirNoContextoAsync(candidata.Unidade, usuarioId, token);

                Interlocked.Add(ref progresso.ProfissionaisEncontrados, atualizado.ProfissionaisEncontrados);
                Interlocked.Add(ref progresso.ProfissionaisNovos, atualizado.ProfissionaisNovos);
                Interlocked.Add(ref progresso.ProcedimentosEncontrados, atualizado.ProcedimentosEncontrados);
                Interlocked.Add(ref progresso.ProcedimentosNovos, atualizado.ProcedimentosNovos);
                Interlocked.Add(ref progresso.PractitionersCriados, fhir.Criados);
                Interlocked.Add(ref progresso.PractitionersVinculados, fhir.Vinculados);
                Interlocked.Increment(ref progresso.UnidadesMapeadas);

                // Existe no SISREG e não tem executante: a central de regulação é o caso típico.
                // Conta como visitada (não volta à fila amanhã) e NÃO conta como erro — chamar isso
                // de falha treina o operador a ignorar a coluna de erro, que é onde mora o que
                // importa de verdade.
                var semProfissionais = atualizado.ProfissionaisEncontrados == 0;

                await RegistrarItemAsync(
                    execucao, candidata,
                    semProfissionais ? ResultadoUnidadeLote.SemProfissionais : ResultadoUnidadeLote.Mapeada,
                    criadasAgora,
                    observacao: semProfissionais
                        ? "Sem profissional executante no SISREG — esperado em unidade que não "
                          + "executa agenda (central de regulação, por exemplo)."
                        : atualizado.ProfissionaisPuladosPorTtl > 0
                            ? $"{atualizado.ProfissionaisPuladosPorTtl} profissionais já estavam "
                              + "atualizados e não custaram requisição."
                            : null,
                    token: token,
                    profissionaisEncontrados: atualizado.ProfissionaisEncontrados,
                    profissionaisNovos: atualizado.ProfissionaisNovos,
                    profissionaisAusentes: atualizado.ProfissionaisAusentes,
                    procedimentosEncontrados: atualizado.ProcedimentosEncontrados,
                    procedimentosNovos: atualizado.ProcedimentosNovos,
                    practitionersCriados: fhir.Criados,
                    practitionersVinculados: fhir.Vinculados,
                    requisicoes: progresso.RequisicoesFeitas - requisicoesAntes);
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
                await RegistrarItemAsync(
                    execucao, candidata, ResultadoUnidadeLote.Erro, criadasAgora,
                    observacao: progresso.UltimoErro, token: token,
                    requisicoes: progresso.RequisicoesFeitas - requisicoesAntes);
                logger.LogError(
                    "SISREG_MAPEAMENTO_LOTE_CAPTCHA: parou em {Unidade} após {Req} requisições, "
                    + "{Feitas}/{Total} unidades.",
                    candidata.Nome, progresso.RequisicoesFeitas, progresso.UnidadesFeitas, progresso.UnidadesTotal);
                Interlocked.Increment(ref progresso.UnidadesFeitas);
                execucao.MensagemErro = progresso.UltimoErro;
                return StatusVarredura.Parcial;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Interlocked.Increment(ref progresso.UnidadesFeitas);
                throw;
            }
            catch (Exception ex)
            {
                // Uma unidade ruim não aborta a rede toda: contabiliza e segue.
                Interlocked.Increment(ref progresso.UnidadesComErro);
                progresso.UltimoErro = $"{candidata.Nome}: {ex.Message}";
                logger.LogWarning(ex, "SISREG_MAPEAMENTO_LOTE: falha na unidade {Unidade}.", candidata.Nome);
                await RegistrarItemAsync(
                    execucao, candidata, ResultadoUnidadeLote.Erro, criadasAgora,
                    observacao: ex.Message, token: token,
                    requisicoes: progresso.RequisicoesFeitas - requisicoesAntes);
            }

            Interlocked.Increment(ref progresso.UnidadesFeitas);
        }

        progresso.UnidadeAtual = null;

        // Uma unidade que falhou é cobertura incompleta, e "Concluída" com erro dentro é
        // exatamente o tipo de rótulo que faz o operador parar de conferir. Mesmo critério do
        // status Parcial da varredura.
        if (progresso.UnidadesComErro > 0) status = StatusVarredura.Parcial;

        return status;
    }

    /// <summary>
    /// Unidades elegíveis, já ordenadas: <b>nunca mapeada primeiro</b>, depois da mais antiga para
    /// a mais nova. É a ordenação que dispensa cursor de retomada — quem ficou de fora numa rodada
    /// é, por construção, quem entra primeiro na seguinte.
    /// </summary>
    private async Task<List<CandidataLote>> CarregarCandidatasAsync(
        IReadOnlySet<string> cnesNoSisreg, CancellationToken ct)
    {
        var unidades = await db.Unidades.AsNoTracking()
            .Where(u => u.Ativo && u.Cnes != null && u.Cnes != "")
            .ToListAsync(ct);

        // Recorte "de lá para cá": só o que existe no SISREG. Unidade que existe só aqui (fechada,
        // de outro fluxo, ou fora do escopo da credencial) não é erro nem merece uma requisição por
        // rodada para confirmar que não está lá. Se a descoberta falhou, o conjunto vem vazio e
        // vale o cadastro inteiro — é o comportamento antigo, que continua sendo o certo às cegas.
        if (cnesNoSisreg.Count > 0)
        {
            unidades = [.. unidades.Where(u => cnesNoSisreg.Contains(SoDigitos(u.Cnes)))];
        }

        if (unidades.Count == 0) return [];

        var agora = DateTime.UtcNow;
        var corteProcedimentos = agora - _opcoes.TtlProcedimentos;

        // Idade do mapeamento e quantos profissionais realmente custariam requisição desta vez.
        var resumo = await db.SisregProfissionaisUnidade.AsNoTracking()
            .GroupBy(p => p.UnidadeId)
            .Select(g => new
            {
                UnidadeId = g.Key,
                MapeadoEm = (DateTime?)g.Max(p => p.VistoEm),
                Profissionais = g.Count(),
                AVencer = g.Count(p => p.ProcedimentosVistosEm == null || p.ProcedimentosVistosEm < corteProcedimentos),
            })
            .ToDictionaryAsync(x => x.UnidadeId, ct);

        // Uma unidade sem profissional nenhum (central de regulação) nunca tem `visto_em`, então
        // pelo `resumo` ela seria eternamente "nunca mapeada" — primeira da fila, 1 requisição por
        // rodada, para sempre. O rastreio do lote é a segunda fonte de "quando visitamos": lá a
        // visita ficou registrada mesmo tendo voltado vazia.
        var visitadaEm = await db.SisregMapeamentoLoteExecucaoItens.AsNoTracking()
            .Where(i => i.Resultado == ResultadoUnidadeLote.Mapeada
                     || i.Resultado == ResultadoUnidadeLote.SemProfissionais)
            .GroupBy(i => i.UnidadeId)
            .Select(g => new { UnidadeId = g.Key, Em = g.Max(i => i.RegistradoEm) })
            .ToDictionaryAsync(x => x.UnidadeId, x => x.Em, ct);

        // A varredura por combinação é a única que depende do mapeamento estar fresco — com o
        // recorte "unidade inteira" a agenda vem numa requisição só, sem olhar o mapeamento.
        var agendas = await db.SisregVarreduraAgendas.AsNoTracking()
            .Select(a => new { a.UnidadeId, a.Ativo, a.RecorteUnidadeInteira })
            .ToDictionaryAsync(x => x.UnidadeId, ct);

        var candidatas = new List<CandidataLote>(unidades.Count);

        foreach (var unidade in unidades)
        {
            resumo.TryGetValue(unidade.Id, out var r);
            var agenda = agendas.GetValueOrDefault(unidade.Id);
            var dependeDoMapeamento = agenda is { Ativo: true, RecorteUnidadeInteira: false };
            var ttlDias = dependeDoMapeamento ? _opcoes.TtlDiasVarreduraPorCombinacao : _opcoes.TtlDiasPadrao;

            // A mais recente das duas fontes: o mapeamento em si e a última visita registrada.
            var mapeadoEm = Maior(r?.MapeadoEm, visitadaEm.GetValueOrDefault(unidade.Id) is var v && v != default ? v : null);
            var precisa = mapeadoEm is null || agora - mapeadoEm.Value >= TimeSpan.FromDays(Math.Max(1, ttlDias));

            // Estimativa do custo: 1 pela lista + 1 por profissional que vai ao SISREG. Uma unidade
            // nunca mapeada não tem histórico, então usa o chute alto do appsettings — subestimar
            // faria o lote entrar numa unidade que não cabe e perder o que já gastou nela.
            var aVencer = r is null ? _opcoes.EstimativaProfissionaisUnidadeNova : r.AVencer;
            var custo = 1 + aVencer + Math.Max(2, aVencer / 10); // margem para profissionais novos

            candidatas.Add(new CandidataLote(
                unidade, mapeadoEm, r?.Profissionais ?? 0, precisa, ttlDias, custo));
        }

        return [.. candidatas
            .OrderBy(c => c.MapeadoEm ?? DateTime.MinValue)
            .ThenBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)];
    }

    private async Task RegistrarItemAsync(
        SisregMapeamentoLoteExecucao execucao,
        CandidataLote candidata,
        ResultadoUnidadeLote resultado,
        IReadOnlySet<Guid> criadasAgora,
        CancellationToken token,
        string? observacao = null,
        int profissionaisEncontrados = 0,
        int profissionaisNovos = 0,
        int profissionaisAusentes = 0,
        int procedimentosEncontrados = 0,
        int procedimentosNovos = 0,
        int practitionersCriados = 0,
        int practitionersVinculados = 0,
        int requisicoes = 0)
    {
        db.SisregMapeamentoLoteExecucaoItens.Add(new SisregMapeamentoLoteExecucaoItem
        {
            Id = Guid.CreateVersion7(),
            ExecucaoId = execucao.Id,
            UnidadeId = candidata.Unidade.Id,
            UnidadeNome = candidata.Nome,
            Cnes = candidata.Unidade.Cnes,
            UnidadeCriada = criadasAgora.Contains(candidata.Unidade.Id),
            Resultado = resultado,
            ProfissionaisEncontrados = profissionaisEncontrados,
            ProfissionaisNovos = profissionaisNovos,
            ProfissionaisAusentes = profissionaisAusentes,
            ProcedimentosEncontrados = procedimentosEncontrados,
            ProcedimentosNovos = procedimentosNovos,
            PractitionersCriados = practitionersCriados,
            PractitionersVinculados = practitionersVinculados,
            Requisicoes = requisicoes,
            Observacao = Truncar(observacao, 1000),
            RegistradoEm = DateTime.UtcNow,
        });

        // Grava a cada unidade: um lote interrompido preserva o detalhe do que já rodou — mesma
        // decisão do rastreio da varredura.
        await db.SaveChangesAsync(token);
    }

    /// <summary>
    /// Fecha execuções que ficaram "Rodando" de um processo que morreu — deploy, restart do
    /// systemd, exceção que escapou. O estado vivo é memória: quem reinicia perde a execução mas
    /// não a linha, e ela ficaria "Rodando" para sempre no histórico, dizendo ao operador que há
    /// algo em curso quando não há. Só chega aqui quem passou pelo <c>EmExecucao</c> do estado
    /// vivo, então não há risco de matar uma execução viva.
    /// </summary>
    private async Task FecharOrfasAsync(DateTime agora, CancellationToken ct)
    {
        var orfas = await db.SisregMapeamentoLoteExecucoes
            .Where(x => x.Status == StatusVarredura.EmExecucao || x.Status == StatusVarredura.Pendente)
            .ToListAsync(ct);

        if (orfas.Count == 0) return;

        foreach (var orfa in orfas)
        {
            orfa.Status = StatusVarredura.Cancelada;
            orfa.MensagemErro ??=
                "A sincronização foi interrompida pelo reinício do serviço. O que já tinha entrado "
                + "está salvo — as unidades que faltaram entram na próxima rodada, na frente da fila.";
            orfa.FinalizadoEm = agora;
            orfa.DuracaoSegundos = (int)Math.Round((agora - orfa.IniciadoEm).TotalSeconds);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "SISREG_MAPEAMENTO_LOTE: {Qtd} execução(ões) órfã(s) fechada(s) como cancelada(s).", orfas.Count);
    }

    private async Task FinalizarAsync(
        SisregMapeamentoLoteExecucao execucao,
        StatusVarredura status,
        ProgressoMapeamentoLote progresso,
        CancellationToken ct)
    {
        var fim = DateTime.UtcNow;

        execucao.Status = status;
        execucao.UnidadesTotal = progresso.UnidadesTotal;
        execucao.UnidadesMapeadas = progresso.UnidadesMapeadas;
        execucao.UnidadesPuladas = progresso.UnidadesPuladas;
        execucao.UnidadesComErro = progresso.UnidadesComErro;
        execucao.ProfissionaisEncontrados = progresso.ProfissionaisEncontrados;
        execucao.ProfissionaisNovos = progresso.ProfissionaisNovos;
        execucao.ProcedimentosEncontrados = progresso.ProcedimentosEncontrados;
        execucao.ProcedimentosNovos = progresso.ProcedimentosNovos;
        execucao.PractitionersCriados = progresso.PractitionersCriados;
        execucao.PractitionersVinculados = progresso.PractitionersVinculados;
        execucao.Requisicoes = progresso.RequisicoesFeitas;
        execucao.MensagemErro ??= progresso.UltimoErro;
        execucao.FinalizadoEm = fim;
        execucao.DuracaoSegundos = (int)Math.Round((fim - execucao.IniciadoEm).TotalSeconds);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Não deixar a falha de gravar o rastreio esconder a falha real do lote.
            logger.LogError(ex, "SISREG_MAPEAMENTO_LOTE: não foi possível gravar o fim da execução {Id}.", execucao.Id);
        }
    }

    public MapeamentoLoteStatusDto? ObterStatus() => estadoVivo.ObterAtual();

    public bool Cancelar() => estadoVivo.Cancelar();

    // ------------------------------------------------------------------ histórico

    public async Task<IReadOnlyList<MapeamentoLoteExecucaoDto>> ListarExecucoesAsync(
        int limite, CancellationToken cancellationToken = default) =>
        await db.SisregMapeamentoLoteExecucoes.AsNoTracking()
            .OrderByDescending(x => x.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(x => new MapeamentoLoteExecucaoDto(
                x.Id, x.Disparo, x.Status,
                x.UnidadesNoSisreg, x.UnidadesCriadas, x.UnidadesComCnesPreenchido,
                x.UnidadesTotal, x.UnidadesMapeadas, x.UnidadesPuladas, x.UnidadesComErro,
                x.ProfissionaisEncontrados, x.ProfissionaisNovos,
                x.ProcedimentosEncontrados, x.ProcedimentosNovos,
                x.PractitionersCriados, x.PractitionersVinculados,
                x.Requisicoes, x.MensagemErro, x.IniciadoEm, x.FinalizadoEm, x.DuracaoSegundos))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MapeamentoLoteExecucaoItemDto>> ListarItensAsync(
        Guid execucaoId, CancellationToken cancellationToken = default)
    {
        var existe = await db.SisregMapeamentoLoteExecucoes
            .AnyAsync(x => x.Id == execucaoId, cancellationToken);
        if (!existe) throw new NaoEncontradoException("Sincronização do SISREG", execucaoId);

        var itens = await db.SisregMapeamentoLoteExecucaoItens.AsNoTracking()
            .Where(x => x.ExecucaoId == execucaoId)
            .Select(x => new MapeamentoLoteExecucaoItemDto(
                x.Id, x.UnidadeId, x.UnidadeNome, x.Cnes, x.UnidadeCriada, x.Resultado,
                x.ProfissionaisEncontrados, x.ProfissionaisNovos, x.ProfissionaisAusentes,
                x.ProcedimentosEncontrados, x.ProcedimentosNovos,
                x.PractitionersCriados, x.PractitionersVinculados,
                x.Requisicoes, x.Observacao))
            .ToListAsync(cancellationToken);

        // Ordena em memória (dezenas de linhas): o que EXIGE ação vem primeiro. Ordenar pelo valor
        // do enum jogaria os erros para o fim da tabela — bem o que o operador abriu o detalhe para
        // achar. Depois as mapeadas (as que mais renderam antes) e, por último, as puladas, que são
        // o caso normal e não pedem nada de ninguém.
        static int Rank(ResultadoUnidadeLote r) => r switch
        {
            ResultadoUnidadeLote.Erro => 0,
            ResultadoUnidadeLote.Mapeada => 1,
            ResultadoUnidadeLote.SomenteDescoberta => 2,
            ResultadoUnidadeLote.PuladaPorOrcamento => 3,
            _ => 4,
        };

        return [.. itens
            .OrderBy(x => Rank(x.Resultado))
            .ThenByDescending(x => x.ProfissionaisEncontrados)
            .ThenBy(x => x.UnidadeNome, StringComparer.OrdinalIgnoreCase)];
    }

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

    /// <summary>Uma unidade candidata do lote, já com a decisão de TTL e o custo estimado.</summary>
    private sealed record CandidataLote(
        Unidade Unidade,
        DateTime? MapeadoEm,
        int ProfissionaisConhecidos,
        bool PrecisaMapear,
        int TtlDias,
        int CustoEstimado)
    {
        public string Nome => Unidade.Nome;
    }

    private static DateTime? Maior(DateTime? a, DateTime? b) =>
        a is null ? b : b is null ? a : (a > b ? a : b);

    private static string SoDigitos(string? valor) =>
        new([.. (valor ?? string.Empty).Where(char.IsDigit)]);

    private static string? Truncar(string? texto, int max) =>
        texto is not null && texto.Length > max ? texto[..max] : texto;

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
