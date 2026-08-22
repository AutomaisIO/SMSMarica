using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Pacientes.Agendamentos.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Pacientes.Agendamentos;

/// <inheritdoc />
public sealed partial class AgendamentosPacienteService(SmsMaricaDbContext db)
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
        itens.AddRange(await LerSisregAsync(pacienteId, cancellationToken));
        itens.AddRange(await LerLocalAsync(pacienteId, cancellationToken));

        var proximos = itens.Where(i => !EhHistorico(i, hojeLocal))
            .OrderBy(i => i.DataHora is null ? 1 : 0)
            .ThenBy(i => i.DataHora)
            .ToList();

        var historico = itens.Where(i => EhHistorico(i, hojeLocal))
            .OrderBy(i => i.DataHora is null ? 1 : 0)
            .ThenByDescending(i => i.DataHora)
            .ToList();

        return new AgendamentosPacienteDto(proximos, historico);
    }

    /// <summary>Situações que encerram o ciclo — vão sempre para o histórico, mesmo com data
    /// futura (ex.: um agendamento cancelado que ainda não passou).</summary>
    private static bool EhTerminal(SituacaoAgendamentoPaciente s) => s is
        SituacaoAgendamentoPaciente.Compareceu
        or SituacaoAgendamentoPaciente.ChegadaNaoConfirmada
        or SituacaoAgendamentoPaciente.Faltou
        or SituacaoAgendamentoPaciente.Cancelado
        or SituacaoAgendamentoPaciente.Concluido;

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
                x.Tipo,
                x.Recurso,
                x.UnidadeExecutora,
                x.AgendadoParaTexto,
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
                l.Recurso,
                l.UnidadeExecutora ?? unidadeTexto,
                data,
                temHora,
                situacao,
                DescreverSituacao(situacao),
                l.Situacao.ToString());
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

    // ---- SISREG / regulação municipal (solicitacao) ----

    private async Task<IEnumerable<AgendamentoPacienteItemDto>> LerSisregAsync(
        Guid pacienteId, CancellationToken cancellationToken)
    {
        var linhas = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == pacienteId && s.ExcluidoEm == null)
            .Select(s => new
            {
                s.Id,
                s.Categoria,
                s.ProcedimentoTexto,
                s.EspecialidadeTexto,
                UnidadeNome = s.UnidadeExecutante != null ? s.UnidadeExecutante.Nome : null,
                s.DataAgendada,
                s.Status,
                s.AutorizadoEm,
            })
            .ToListAsync(cancellationToken);

        return linhas.Select(l =>
        {
            // AutorizadoEm = a recepção registrou a chegada presencial (comparecimento).
            var situacao = l.AutorizadoEm is not null
                ? SituacaoAgendamentoPaciente.Compareceu
                : MapearSisreg(l.Status);
            var data = l.DataAgendada is { } d
                ? DateTime.SpecifyKind(FusoBrasilia.ParaExibicao(d), DateTimeKind.Unspecified)
                : (DateTime?)null;
            return new AgendamentoPacienteItemDto(
                l.Id,
                OrigemAgendamentoPaciente.Sisreg,
                l.Categoria == CategoriaSolicitacao.Consulta ? "Consulta" : "Exame",
                l.ProcedimentoTexto ?? l.EspecialidadeTexto ?? "Procedimento",
                l.UnidadeNome,
                data,
                data is not null, // DataAgendada carrega horário
                situacao,
                DescreverSituacao(situacao),
                l.Status.ToString());
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

    // ---- Agenda própria do município (agendamento) ----

    private async Task<IEnumerable<AgendamentoPacienteItemDto>> LerLocalAsync(
        Guid pacienteId, CancellationToken cancellationToken)
    {
        var linhas = await db.Agendamentos.AsNoTracking()
            .Where(a => a.PacienteId == pacienteId && a.ExcluidoEm == null)
            .Select(a => new
            {
                a.Id,
                a.TipoExameId,
                a.InicioEm,
                a.Status,
                EspecialidadeNome = a.Agenda!.Especialidade != null ? a.Agenda.Especialidade.Nome : null,
                TipoExameNome = a.TipoExame != null ? a.TipoExame.Nome : null,
                UnidadeNome = a.Agenda!.Unidade != null ? a.Agenda.Unidade.Nome : null,
            })
            .ToListAsync(cancellationToken);

        return linhas.Select(l =>
        {
            var ehExame = l.TipoExameId is not null;
            var situacao = MapearLocal(l.Status);
            return new AgendamentoPacienteItemDto(
                l.Id,
                OrigemAgendamentoPaciente.Local,
                ehExame ? "Exame" : "Consulta",
                (ehExame ? l.TipoExameNome : l.EspecialidadeNome) ?? (ehExame ? "Exame" : "Consulta"),
                l.UnidadeNome,
                // InicioEm já é wall-clock de Brasília (timestamp without time zone).
                DateTime.SpecifyKind(l.InicioEm, DateTimeKind.Unspecified),
                true,
                situacao,
                DescreverSituacao(situacao),
                l.Status.ToString());
        });
    }

    private static SituacaoAgendamentoPaciente MapearLocal(StatusAgendamento s) => s switch
    {
        StatusAgendamento.Agendado => SituacaoAgendamentoPaciente.Agendado,
        StatusAgendamento.Confirmado => SituacaoAgendamentoPaciente.Confirmado,
        StatusAgendamento.Realizado => SituacaoAgendamentoPaciente.Compareceu,
        StatusAgendamento.Cancelado => SituacaoAgendamentoPaciente.Cancelado,
        StatusAgendamento.Faltou => SituacaoAgendamentoPaciente.Faltou,
        _ => SituacaoAgendamentoPaciente.Agendado,
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

    [GeneratedRegex(@"(?<data>\d{2}/\d{2}/\d{4})(?:\s+(?<hh>\d{2}):(?<mm>\d{2}))?")]
    private static partial Regex RegexAgendadoPara();
}
