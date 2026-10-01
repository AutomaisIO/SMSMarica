using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.AgendaPontual;
using SMSMais.Core.Pacientes.Agendamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Pacientes.Agendamentos;

/// <inheritdoc />
public sealed partial class AgendamentosPacienteService(SmsMaisDbContext db)
    : IAgendamentosPacienteService
{
    public async Task<AgendamentosPacienteDto> ListarPorPacienteAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        // Referência de "hoje" em Brasília — o eixo futuro/passado é a data do AGENDAMENTO,
        // não a situação: há registros "Agendada" com data de 2020 que já são passado.
        var hojeLocal = FusoBrasilia.ParaExibicao(DateTime.UtcNow).Date;

        var itens = new List<AgendamentoPacienteItemDto>();
        itens.AddRange(await LerSerAsync(pacienteId, cancellationToken));
        itens.AddRange(await LerSernitAsync(pacienteId, cancellationToken));
        itens.AddRange(await LerEsusSgAsync(pacienteId, cancellationToken));
        itens.AddRange(await LerSisregAsync(pacienteId, DateOnly.FromDateTime(hojeLocal), cancellationToken));
        // A agenda local do municipio foi removida em 05/09/2026 (ver CidadaoClinicoService):
        // tinha 3 linhas de teste e um modelo incompativel com a grade do SISREG. Sobra o que o
        // cidadao realmente tem marcado — o que veio da regulacao.

        var proximos = itens.Where(i => !EhHistorico(i, hojeLocal))
            .OrderBy(i => i.DataHora is null ? 1 : 0)
            .ThenBy(i => i.DataHora)
            .ToList();

        var historico = itens.Where(i => EhHistorico(i, hojeLocal))
            // "Agendado" numa data que já passou não responde o que o operador quer saber (veio ou
            // não veio?). Quem chegou até aqui sem prova de chegada nem de falta diz isso com todas
            // as letras, em vez de parecer um agendamento ainda por acontecer.
            .Select(i => i.Situacao is SituacaoAgendamentoPaciente.Agendado or SituacaoAgendamentoPaciente.Confirmado
                ? i with
                {
                    Situacao = SituacaoAgendamentoPaciente.SemRegistroDeChegada,
                    SituacaoDescricao = DescreverSituacao(SituacaoAgendamentoPaciente.SemRegistroDeChegada),
                }
                : i)
            .OrderBy(i => i.DataHora is null ? 1 : 0)
            .ThenByDescending(i => i.DataHora)
            .ToList();

        return new AgendamentosPacienteDto(proximos, historico);
    }

    /// <summary>Situações que encerram o ciclo — vão sempre para o histórico, mesmo com data
    /// futura (ex.: um agendamento cancelado que ainda não passou).</summary>
    public async Task<IReadOnlyList<AgendamentoPacienteItemDto>> RegulacaoParaPacienteAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var hojeLocal = FusoBrasilia.ParaExibicao(DateTime.UtcNow).Date;
        var itens = new List<AgendamentoPacienteItemDto>();
        itens.AddRange(await LerSerAsync(pacienteId, cancellationToken));
        itens.AddRange(await LerSernitAsync(pacienteId, cancellationToken));
        itens.AddRange(await LerEsusSgAsync(pacienteId, cancellationToken));

        return itens
            .Where(i => !EhHistorico(i, hojeLocal))
            .Where(i => i.Situacao is SituacaoAgendamentoPaciente.EmFila or SituacaoAgendamentoPaciente.Pendente
                || (i.Situacao is SituacaoAgendamentoPaciente.Agendado or SituacaoAgendamentoPaciente.Confirmado
                    && i.DataHora is not null))
            // Pendência vira "na fila": o motivo nunca sai para o paciente.
            .Select(i => i.Situacao == SituacaoAgendamentoPaciente.Pendente
                ? i with { Situacao = SituacaoAgendamentoPaciente.EmFila, SituacaoDescricao = DescreverSituacao(SituacaoAgendamentoPaciente.EmFila), SituacaoOrigem = null }
                : i)
            .OrderBy(i => i.DataHora is null ? 1 : 0)
            .ThenBy(i => i.DataHora)
            .ToList();
    }

    /// <summary>Como o paciente entende quem marcou/regula o pedido (nunca a sigla crua sozinha).</summary>
    public static string DescreverRegulacaoParaPaciente(OrigemAgendamentoPaciente origem) => origem switch
    {
        OrigemAgendamentoPaciente.Ser => "regulação estadual (SER)",
        OrigemAgendamentoPaciente.Sernit => "regulação de Niterói",
        OrigemAgendamentoPaciente.EsusSg => "regulação de São Gonçalo",
        _ => "regulação",
    };

    private static bool EhTerminal(SituacaoAgendamentoPaciente s) => s is
        SituacaoAgendamentoPaciente.Compareceu
        or SituacaoAgendamentoPaciente.ChegadaNaoConfirmada
        or SituacaoAgendamentoPaciente.Faltou
        or SituacaoAgendamentoPaciente.Cancelado
        or SituacaoAgendamentoPaciente.Concluido
        or SituacaoAgendamentoPaciente.SaiuDaFila
        or SituacaoAgendamentoPaciente.SemRegistroDeChegada
        or SituacaoAgendamentoPaciente.EmAberto;

    private static bool EhHistorico(AgendamentoPacienteItemDto i, DateTime hojeLocal)
    {
        // Situações terminais são sempre histórico, mesmo com data futura (ex.: cancelado que
        // ainda não passou).
        if (EhTerminal(i.Situacao)) return true;

        // Espera (em fila / pendente) é trabalho ativo de regulação: fica SEMPRE em "próximos",
        // mesmo que carregue uma data velha no texto — esconder um pendente no histórico faria o
        // operador achar que não há nada por resolver.
        if (i.Situacao is SituacaoAgendamentoPaciente.EmFila or SituacaoAgendamentoPaciente.Pendente)
            return false;

        // Agendado/Confirmado: quem já passou vai para o histórico; sem data, é algo por vir.
        return i.DataHora is { } dh && dh.Date < hojeLocal;
    }

    // ---- SER (regulação estadual) ----

    private async Task<IEnumerable<AgendamentoPacienteItemDto>> LerSerAsync(
        Guid pacienteId, CancellationToken cancellationToken)
    {
        var linhas = await db.SerSolicitacoes.AsNoTracking()
            .Where(x => x.PacienteId == pacienteId && x.ExcluidoEm == null)
            .Select(x => new
            {
                x.Id,
                x.IdSer,
                x.Tipo,
                x.Recurso,
                x.UnidadeExecutora,
                x.AgendadoParaTexto,
                x.DataSolicitacao,
                x.Situacao,
            })
            .ToListAsync(cancellationToken);

        return linhas.Select(l =>
        {
            var (data, temHora, unidadeTexto) = ParsearAgendadoPara(l.AgendadoParaTexto);
            var situacao = MapearSer(l.Situacao);
            return new AgendamentoPacienteItemDto(
                l.Id,
                OrigemAgendamentoPaciente.Ser,
                l.Tipo == TipoRecursoSer.Consulta ? "Consulta" : "Exame",
                NormalizarTexto(l.Recurso),
                l.UnidadeExecutora ?? unidadeTexto,
                data,
                temHora,
                l.DataSolicitacao,
                situacao,
                DescreverSituacao(situacao),
                l.Situacao.ToString(),
                l.IdSer,
                l.Id); // detalhe SER abre pelo id da própria ser_solicitacao
        });
    }

    private static SituacaoAgendamentoPaciente MapearSer(SituacaoSer s) => s switch
    {
        SituacaoSer.EmFila => SituacaoAgendamentoPaciente.EmFila,
        SituacaoSer.Pendente => SituacaoAgendamentoPaciente.Pendente,
        SituacaoSer.Agendada => SituacaoAgendamentoPaciente.Agendado,
        SituacaoSer.ChegadaNaoConfirmada => SituacaoAgendamentoPaciente.ChegadaNaoConfirmada,
        SituacaoSer.ChegadaConfirmada => SituacaoAgendamentoPaciente.Compareceu,
        SituacaoSer.Cancelada => SituacaoAgendamentoPaciente.Cancelado,
        SituacaoSer.Alta => SituacaoAgendamentoPaciente.Concluido,
        _ => SituacaoAgendamentoPaciente.Pendente,
    };

    // ---- SERNIT (regulação de Niterói — subsistema irmão do SER, ADR-0042) ----

    private async Task<IEnumerable<AgendamentoPacienteItemDto>> LerSernitAsync(
        Guid pacienteId, CancellationToken cancellationToken)
    {
        var linhas = await db.SernitSolicitacoes.AsNoTracking()
            .Where(x => x.PacienteId == pacienteId && x.ExcluidoEm == null)
            .Select(x => new
            {
                x.Id,
                x.IdSernit,
                x.Tipo,
                x.Recurso,
                x.UnidadeExecutora,
                x.AgendadoParaTexto,
                x.DataSolicitacao,
                x.Situacao,
            })
            .ToListAsync(cancellationToken);

        return linhas.Select(l =>
        {
            var (data, temHora, unidadeTexto) = ParsearAgendadoPara(l.AgendadoParaTexto);
            var situacao = MapearSernit(l.Situacao);
            return new AgendamentoPacienteItemDto(
                l.Id,
                OrigemAgendamentoPaciente.Sernit,
                l.Tipo == TipoRecursoSernit.Consulta ? "Consulta" : "Exame",
                NormalizarTexto(l.Recurso),
                l.UnidadeExecutora ?? unidadeTexto,
                data,
                temHora,
                l.DataSolicitacao,
                situacao,
                DescreverSituacao(situacao),
                l.Situacao.ToString(),
                l.IdSernit,
                l.Id); // detalhe SERNIT abre pelo id da própria sernit_solicitacao
        });
    }

    private static SituacaoAgendamentoPaciente MapearSernit(SituacaoSernit s) => s switch
    {
        SituacaoSernit.EmFila => SituacaoAgendamentoPaciente.EmFila,
        SituacaoSernit.Pendente => SituacaoAgendamentoPaciente.Pendente,
        SituacaoSernit.Agendada => SituacaoAgendamentoPaciente.Agendado,
        SituacaoSernit.ChegadaNaoConfirmada => SituacaoAgendamentoPaciente.ChegadaNaoConfirmada,
        SituacaoSernit.ChegadaConfirmada => SituacaoAgendamentoPaciente.Compareceu,
        SituacaoSernit.Cancelada => SituacaoAgendamentoPaciente.Cancelado,
        SituacaoSernit.Alta => SituacaoAgendamentoPaciente.Concluido,
        _ => SituacaoAgendamentoPaciente.Pendente,
    };

    // ---- ESUS de São Gonçalo (ADR-0063) — PPI de exame de Maricá em SG ----

    private async Task<IEnumerable<AgendamentoPacienteItemDto>> LerEsusSgAsync(
        Guid pacienteId, CancellationToken cancellationToken)
    {
        var linhas = await db.EsusSgSolicitacoes.AsNoTracking()
            .Where(x => x.PacienteId == pacienteId && x.ExcluidoEm == null)
            .Select(x => new
            {
                x.Id,
                x.IdEsusSg,
                x.Tipo,
                x.Recurso,
                x.UnidadeExecutora,
                x.DataAgendada,
                x.DataHoraAgendadaTexto,
                x.DataSolicitacao,
                x.DataEntradaFila,
                x.Situacao,
                x.Efetivacao,
                x.EfetivadoEm,
                x.MotivoNaoEfetivacao,
                x.EfetivacaoLidaEm,
            })
            .ToListAsync(cancellationToken);

        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        return linhas.Select(l =>
        {
            // "06/10/2026 13:15:00" é hora LOCAL de Brasília — fica wall-clock, como no SER.
            var temHora = DateTime.TryParseExact(l.DataHoraAgendadaTexto?.Trim(),
                ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dataHora);
            DateTime? data = temHora ? dataHora : l.DataAgendada?.ToDateTime(TimeOnly.MinValue);
            var situacao = MapearEsusSg(l.Situacao);
            var agendado = situacao == SituacaoAgendamentoPaciente.Agendado;
            var prova = l.Situacao.ToString();

            // O que a unidade executante apontou no ESUS (lido do histórico do paciente pela varredura).
            if (agendado && l.DataAgendada is { } dia && dia < hoje)
            {
                switch (l.Efetivacao)
                {
                    case EfetivacaoEsusSg.Efetivado:
                        situacao = SituacaoAgendamentoPaciente.Compareceu;
                        prova = l.EfetivadoEm is { } em
                            ? $"exame efetivado pela unidade no ESUS em {FusoBrasilia.ParaExibicao(em):dd/MM/yyyy}"
                            : "exame efetivado pela unidade no ESUS";
                        break;
                    case EfetivacaoEsusSg.NaoEfetivado:
                        situacao = SituacaoAgendamentoPaciente.Faltou;
                        prova = "exame não efetivado no ESUS" + (l.MotivoNaoEfetivacao is { } m ? $": {m}" : string.Empty);
                        break;
                    default:
                        // Em aberto só se alguém foi olhar DEPOIS do dia; sem isso, "sem registro".
                        if (l.Efetivacao == EfetivacaoEsusSg.EmAberto
                            || (l.EfetivacaoLidaEm is { } lida && DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(lida)) > dia))
                        {
                            situacao = SituacaoAgendamentoPaciente.EmAberto;
                            prova = l.EfetivacaoLidaEm is { } quando
                                ? $"sem apontamento da unidade no ESUS em {FusoBrasilia.ParaExibicao(quando):dd/MM/yyyy}"
                                : "sem apontamento da unidade no ESUS";
                        }
                        break;
                }
            }

            return new AgendamentoPacienteItemDto(
                l.Id,
                OrigemAgendamentoPaciente.EsusSg,
                l.Tipo == TipoRecursoEsusSg.Consulta ? "Consulta" : "Exame",
                NormalizarTexto(l.Recurso),
                l.UnidadeExecutora,
                agendado ? data : null,
                temHora && agendado,
                l.DataSolicitacao ?? l.DataEntradaFila,
                situacao,
                DescreverSituacao(situacao),
                prova,
                l.IdEsusSg,
                l.Id); // detalhe ESUS SG abre pelo id da própria esussg_solicitacao
        });
    }

    private static SituacaoAgendamentoPaciente MapearEsusSg(SituacaoEsusSg s) => s switch
    {
        SituacaoEsusSg.EmFila => SituacaoAgendamentoPaciente.EmFila,
        SituacaoEsusSg.Pendente => SituacaoAgendamentoPaciente.Pendente,
        SituacaoEsusSg.Agendada => SituacaoAgendamentoPaciente.Agendado,
        SituacaoEsusSg.SaiuDaFila => SituacaoAgendamentoPaciente.SaiuDaFila,
        _ => SituacaoAgendamentoPaciente.Pendente,
    };

    // ---- SISREG / regulação municipal (solicitacao) ----

    private async Task<IEnumerable<AgendamentoPacienteItemDto>> LerSisregAsync(
        Guid pacienteId, DateOnly hoje, CancellationToken cancellationToken)
    {
        var linhas = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == pacienteId && s.ExcluidoEm == null)
            .Select(s => new
            {
                s.Id,
                s.Categoria,
                s.CodigoSolicitacao,
                s.ProcedimentoTexto,
                s.EspecialidadeTexto,
                UnidadeNome = s.UnidadeExecutante != null ? s.UnidadeExecutante.Nome : null,
                s.DataAgendada,
                s.DataSolicitacao,
                s.Status,
                s.AutorizadoEm,
                s.RawSisreg,
                s.ChegadaConfirmadaSisreg,
                s.ChegadaSisregLidaEm,
                // O detalhe (GET /solicitacoes-exame/{id}) resolve pelo id do satélite de imagem —
                // existe só para exame de imagem; consulta/gráfico/outros ficam sem modal.
                ExameImagemId = s.ExameImagem != null ? (Guid?)s.ExameImagem.Id : null,
            })
            .ToListAsync(cancellationToken);

        // Comparecimento só se discute de quem segue "Agendada" e tem código de verdade no SISREG:
        // é pelo código que a lista oficial de faltas casa ('0000' é o marcador de pedido sem código).
        var codigos = linhas
            .Where(l => l.Status == StatusSolicitacao.Agendada && l.AutorizadoEm is null
                && !string.IsNullOrWhiteSpace(l.CodigoSolicitacao) && l.CodigoSolicitacao != "0000")
            .Select(l => l.CodigoSolicitacao!)
            .Distinct()
            .ToList();

        // O SISREG tem TRÊS estados para um agendamento que já passou, e a unidade executante é quem
        // escolhe: Confirmado, Falta, ou Pendente de confirmação (ainda não apontou nada). Conferido
        // em 01/10/2026 contra a tela de agenda do CDT capturada em 25/07 (685 agendamentos passados):
        //   - 155 das 156 "Falta" estão na lista de absenteísmo; das 108 "Pendente", só 4 (as que a
        //     unidade apontou como falta DEPOIS) — a lista é a marcação explícita de falta, não
        //     "quem não foi confirmado";
        //   - 104 das 108 "Pendente" seguiam pendentes seis semanas depois: pendente não vira falta
        //     sozinho. É "a unidade não disse", e não pode aparecer como ausência do paciente.
        // O Arquivo de Agendamentos (coluna 34) só separa CONFIRMADO do resto — falta e pendente saem
        // os dois como "PENDENTE" —, por isso a falta só vem da lista.
        //
        // Código + DIA: o mesmo código pode faltar, ser remarcado e comparecer na data nova.
        var faltas = new Dictionary<(string Codigo, DateOnly Dia), DateTime>();
        var janelasLidas = new List<(DateOnly Inicio, DateOnly Fim)>();
        if (codigos.Count > 0)
        {
            faltas = (await db.SisregFaltasOficiais.AsNoTracking()
                    .Where(f => codigos.Contains(f.CodigoSolicitacao))
                    .Select(f => new { f.CodigoSolicitacao, f.DataExecucao, f.LidoEm })
                    .ToListAsync(cancellationToken))
                .ToDictionary(f => (f.CodigoSolicitacao, f.DataExecucao), f => f.LidoEm);

            // "Não está na lista de faltas" só significa algo nos dias em que a lista foi LIDA — pela
            // leitura das semanas recentes (de hora em hora) ou pela que alimenta o indicador (30 dias).
            janelasLidas = (await db.SisregIndicadorColetas.AsNoTracking()
                    .Where(c => (c.Coletor == ColetorIndicadorSisreg.Faltas
                                 || c.Coletor == ColetorIndicadorSisreg.FaltasRecentes)
                        && c.Status == StatusColetaIndicador.Concluida && c.JanelaInicio < hoje)
                    .Select(c => new { c.JanelaInicio, c.JanelaFim })
                    .ToListAsync(cancellationToken))
                .Select(c => (c.JanelaInicio, c.JanelaFim))
                .ToList();
        }

        return linhas.Select(l =>
        {
            var data = l.DataAgendada is { } d
                ? DateTime.SpecifyKind(FusoBrasilia.ParaExibicao(d), DateTimeKind.Unspecified)
                : (DateTime?)null;

            var situacao = MapearSisreg(l.Status);
            var prova = l.Status.ToString();
            if (l.AutorizadoEm is not null)
            {
                // AutorizadoEm = a recepção registrou a chegada presencial (comparecimento).
                situacao = SituacaoAgendamentoPaciente.Compareceu;
                prova = "chegada registrada na recepção";
            }
            else if (l.Status == StatusSolicitacao.Agendada && data is { } dataHora
                && DateOnly.FromDateTime(dataHora) is var dia && dia <= hoje)
            {
                // A coluna relida pela varredura manda; sem ela, vale o CONFIRMADO da linha guardada
                // (histórico carregado depois do fato). O "pendente" da linha guardada não vale nada:
                // costuma ser de ANTES do atendimento.
                var confirmada = l.ChegadaConfirmadaSisreg
                    ?? (AgendaTxtParser.ChegadaConfirmada(l.RawSisreg) == true ? true : (bool?)null);
                // Envelope da Consulta de Agendas: a tela traz os três estados por extenso.
                var naTela = ConsAgendasParser.ChegadaNoEnvelope(l.RawSisreg);

                var temCodigo = l.CodigoSolicitacao is { } c && codigos.Contains(c);

                if (confirmada == true || naTela == true)
                {
                    situacao = SituacaoAgendamentoPaciente.Compareceu;
                    prova = "chegada confirmada pela unidade executante no SISREG";
                }
                else if (temCodigo && faltas.TryGetValue((l.CodigoSolicitacao!, dia), out var faltaLidaEm))
                {
                    situacao = SituacaoAgendamentoPaciente.Faltou;
                    prova = "falta registrada pela unidade executante no SISREG (lista de faltas lida em "
                        + $"{FusoBrasilia.ParaExibicao(faltaLidaEm):dd/MM/yyyy})";
                }
                else if (naTela == false)
                {
                    situacao = SituacaoAgendamentoPaciente.Faltou;
                    prova = "falta registrada pela unidade executante no SISREG";
                }
                else if (dia < hoje)
                {
                    // Nem confirmado nem falta. Só se afirma "em aberto" quando alguém foi olhar DEPOIS
                    // do dia: a lista de faltas daquele dia foi lida, ou a varredura releu a chegada.
                    var listaLida = temCodigo && janelasLidas.Any(j => dia >= j.Inicio && dia <= j.Fim);
                    var chegadaRelida = l.ChegadaConfirmadaSisreg == false && l.ChegadaSisregLidaEm is { } lida
                        && DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(lida)) > dia;
                    if (listaLida || chegadaRelida)
                    {
                        situacao = SituacaoAgendamentoPaciente.EmAberto;
                        prova = chegadaRelida
                            ? "sem apontamento da unidade no SISREG em "
                              + $"{FusoBrasilia.ParaExibicao(l.ChegadaSisregLidaEm!.Value):dd/MM/yyyy}"
                            : "sem apontamento da unidade no SISREG";
                    }
                }
            }

            return new AgendamentoPacienteItemDto(
                l.Id,
                OrigemAgendamentoPaciente.Sisreg,
                l.Categoria == CategoriaSolicitacao.Consulta ? "Consulta" : "Exame",
                NormalizarTexto(l.ProcedimentoTexto ?? l.EspecialidadeTexto ?? "Procedimento"),
                l.UnidadeNome,
                data,
                data is not null, // DataAgendada carrega horário
                l.DataSolicitacao,
                situacao,
                DescreverSituacao(situacao),
                prova,
                l.CodigoSolicitacao,
                l.ExameImagemId);
        });
    }

    private static SituacaoAgendamentoPaciente MapearSisreg(StatusSolicitacao s) => s switch
    {
        StatusSolicitacao.Solicitada => SituacaoAgendamentoPaciente.Pendente,
        StatusSolicitacao.Agendada => SituacaoAgendamentoPaciente.Agendado,
        StatusSolicitacao.Realizada => SituacaoAgendamentoPaciente.Concluido,
        StatusSolicitacao.Cancelada => SituacaoAgendamentoPaciente.Cancelado,
        _ => SituacaoAgendamentoPaciente.Pendente,
    };

    // ---- Helpers ----

    private static string DescreverSituacao(SituacaoAgendamentoPaciente s) => s switch
    {
        SituacaoAgendamentoPaciente.EmFila => "Em fila",
        SituacaoAgendamentoPaciente.Pendente => "Pendente",
        SituacaoAgendamentoPaciente.Agendado => "Agendado",
        SituacaoAgendamentoPaciente.Confirmado => "Confirmado",
        SituacaoAgendamentoPaciente.Compareceu => "Compareceu",
        SituacaoAgendamentoPaciente.ChegadaNaoConfirmada => "Chegada não confirmada",
        SituacaoAgendamentoPaciente.Faltou => "Faltou",
        SituacaoAgendamentoPaciente.Cancelado => "Cancelado",
        SituacaoAgendamentoPaciente.Concluido => "Concluído",
        SituacaoAgendamentoPaciente.SaiuDaFila => "Saiu da fila",
        SituacaoAgendamentoPaciente.SemRegistroDeChegada => "Sem registro de chegada",
        SituacaoAgendamentoPaciente.EmAberto => "Em aberto",
        _ => s.ToString(),
    };

    /// <summary>
    /// Extrai data/hora e unidade do texto livre "Agendado para" do SER. Formatos observados:
    /// <c>"dd/MM/yyyy"</c> e <c>"dd/MM/yyyy HH:mm - UNIDADE"</c>. Devolve data em wall-clock local
    /// (Unspecified). Texto sem data reconhecível vira <c>(null, false, null)</c> — dado ausente é
    /// melhor que dado inventado.
    /// </summary>
    private static (DateTime? data, bool temHora, string? unidade) ParsearAgendadoPara(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, false, null);

        var m = RegexAgendadoPara().Match(texto);
        if (!m.Success) return (null, false, null);

        var temHora = m.Groups["hh"].Success;
        var formato = temHora ? "dd/MM/yyyy HH:mm" : "dd/MM/yyyy";
        var alvo = temHora
            ? $"{m.Groups["data"].Value} {m.Groups["hh"].Value}:{m.Groups["mm"].Value}"
            : m.Groups["data"].Value;

        if (!DateTime.TryParseExact(alvo, formato, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var data))
            return (null, false, null);

        string? unidade = null;
        var idx = texto.IndexOf(" - ", StringComparison.Ordinal);
        if (idx >= 0)
        {
            var resto = texto[(idx + 3)..].Trim();
            if (resto.Length > 0) unidade = resto;
        }

        return (DateTime.SpecifyKind(data, DateTimeKind.Unspecified), temHora, unidade);
    }

    /// <summary>
    /// Limpa o texto vindo das fontes externas: apara as pontas e colapsa espaços/quebras
    /// repetidos num único espaço (o SER manda "CONSULTA  EM POLISSONOGRAFIA" com espaço duplo).
    /// Preserva a caixa original — converter para Title Case estragaria siglas do vocabulário.
    /// </summary>
    private static string NormalizarTexto(string? t) =>
        string.IsNullOrWhiteSpace(t) ? string.Empty : RegexEspacos().Replace(t.Trim(), " ");

    [GeneratedRegex(@"(?<data>\d{2}/\d{2}/\d{4})(?:\s+(?<hh>\d{2}):(?<mm>\d{2}))?")]
    private static partial Regex RegexAgendadoPara();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}
