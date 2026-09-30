using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Consulta os agendamentos FUTUROS do paciente em DUAS FASES. Fase 1 (sem identidade): diz apenas
/// SE existe agendamento futuro para o contato da conversa — sem nenhum detalhe. Fase 2 (com os 4
/// primeiros dígitos do CPF + mês/ano): confere a identidade e lista. Data, hora e local são
/// sensíveis: não saem sem a identidade conferir.
///
/// A fase 1 nasceu do caso real de 01/09: "estou esperando o ultrassom complementar" — o robô
/// coletou CPF e nascimento para no fim dizer que não tinha nada. Só se pede dado quando HÁ
/// informação para entregar; sem nada, explica-se que a Secretaria entra em contato quando houver
/// e encerra-se. Dizer que EXISTE agendamento não vaza nada além da prática atual (os avisos já
/// chegam proativamente neste mesmo telefone).
///
/// Nasceu de um caso real: uma atendente enviou o agendamento de um cidadão e, dias depois, o robô
/// — sem ferramenta nenhuma de agendamento — respondeu que "não há agendamento futuro registrado",
/// desmentindo a própria Secretaria. Faltava a ferramenta; ele preencheu o vazio com uma afirmação.
///
/// Quando não encontra, a mensagem deixa explícito que isso significa "não localizei no NOSSO
/// sistema", não "não existe": a base é uma visão parcial e a importação da regulação é assíncrona.
///
/// Cobre as TRÊS fontes: solicitações locais (SISREG) por data futura, e os espelhos SER e SERNIT
/// (regulação estadual/Niterói) por situação Agendada — lá a data vem em TEXTO ("Agendado para"),
/// então é exibida como está, sem filtro por data.
///
/// Também devolve os CANCELADOS recentes com o motivo que a atendente registrou PARA o paciente
/// (regra perene, 29/09/2026): "por que cancelou?" se responde lendo esse motivo, nunca
/// inventando nem mandando a pessoa "perguntar no posto" quando a resposta está registrada.
/// </summary>
public sealed class ConsultarAgendamentosComando(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    PendenciasCadastro.IContatoNegadoService contatosNegados) : IRoboComando
{
    private const int Maximo = 5;

    /// <summary>Até quando um cancelamento ainda interessa na conversa: quem pergunta "por que
    /// cancelou?" pergunta de algo recente — 60 dias cobrem o aviso e a dúvida que vem depois.</summary>
    private const int JanelaCanceladosDias = 60;

    public ComandoRobo Comando => ComandoRobo.ConsultarStatusAgendamento;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:consultar_agendamentos";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        var cpf = GateIdentidade.LerString(ctx.Args, "cpf");
        var mes = GateIdentidade.LerInt(ctx.Args, "mesNascimento");
        var ano = GateIdentidade.LerInt(ctx.Args, "anoNascimento");

        // FASE 1 — sem NENHUM dado: responde só SE existe, e o que fazer em seguida.
        if (string.IsNullOrWhiteSpace(cpf) && mes is null && ano is null)
            return await VerificarExistenciaAsync(ctx, ct);

        // Dados parciais: falta ≠ erro — peça o que falta, não conclua nada.
        if (string.IsNullOrWhiteSpace(cpf) || GateIdentidade.SoDigitos(cpf).Length < 4)
            return new(false, "Faltam os dígitos do CPF. Peça os *4 primeiros dígitos do CPF* do paciente, "
                + "todos de uma vez, e chame de novo. NÃO conclua nada sobre agendamento.");
        if (mes is null || ano is null)
            return new(false, "Falta a data de nascimento. Peça o MÊS e o ANO de nascimento do paciente e "
                + "chame de novo. NÃO conclua nada sobre agendamento.");

        var alvo = await ResolverPacienteAsync(ctx, cpf, mes, ano, ct);
        if (alvo is null)
            return new(false,
                "Não consegui conferir a identidade para ver agendamentos. NÃO diga que a pessoa não tem "
                + "agendamento — você não chegou a consultar. Peça os dados novamente ou encaminhe.");

        var agora = DateTime.UtcNow;
        var futuros = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == alvo.Value && s.ExcluidoEm == null
                // Cancelado não é agendamento de pé: listá-lo como ativo fazia o robô
                // "confirmar" uma vaga que a equipe acabara de derrubar.
                && s.Status != StatusSolicitacao.Cancelada
                && s.DataAgendada != null && s.DataAgendada >= agora)
            .OrderBy(s => s.DataAgendada)
            .Take(Maximo)
            .Select(s => new
            {
                s.DataAgendada,
                Procedimento = s.ExameImagem != null && s.ExameImagem.TipoExame != null
                    ? s.ExameImagem.TipoExame.Nome
                    : (s.EspecialidadeTexto ?? s.ProcedimentoTexto),
                Unidade = s.UnidadeExecutante != null ? s.UnidadeExecutante.Nome : null,
                s.UnidadeExecutanteId,
                s.StatusConfirmacao,
            })
            .ToListAsync(ct);

        // Cancelados recentes, com o motivo REDIGIDO PARA o paciente (campo próprio do modal de
        // cancelar). Regra perene: quando a pessoa pergunta por que cancelou, o robô LÊ esse
        // motivo — não manda "perguntar no posto" se a resposta está registrada.
        var cancelados = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == alvo.Value && s.ExcluidoEm == null
                && s.Status == StatusSolicitacao.Cancelada
                && s.CanceladoEm != null && s.CanceladoEm >= agora.AddDays(-JanelaCanceladosDias))
            .OrderByDescending(s => s.CanceladoEm)
            .Take(Maximo)
            .Select(s => new
            {
                s.DataAgendada,
                s.CanceladoEm,
                Procedimento = s.ExameImagem != null && s.ExameImagem.TipoExame != null
                    ? s.ExameImagem.TipoExame.Nome
                    : (s.EspecialidadeTexto ?? s.ProcedimentoTexto),
                s.MotivoCancelamentoParaPaciente,
            })
            .ToListAsync(ct);

        // Campanha (ADR-0062): o local e o endereço da campanha valem sobre a unidade do SISREG —
        // senão o robô mandaria a paciente da Carreta para a Secretaria.
        var campanhas = new Dictionary<int, Notificacoes.Campanhas.CampanhaVigente?>();
        for (var i = 0; i < futuros.Count; i++)
            campanhas[i] = await Notificacoes.Campanhas.CampanhaResolver.VigenteAsync(
                db, futuros[i].UnidadeExecutanteId, futuros[i].DataAgendada, ct);

        var linhasSer = await db.SerSolicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == alvo.Value && s.ExcluidoEm == null
                && s.Situacao == Data.Entities.Ser.SituacaoSer.Agendada)
            .OrderByDescending(s => s.AtualizadoEm ?? s.CriadoEm)
            .Take(Maximo)
            .Select(s => new { s.Recurso, s.AgendadoParaTexto, s.UnidadeExecutora })
            .ToListAsync(ct);
        var linhasSernit = await db.SernitSolicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == alvo.Value && s.ExcluidoEm == null
                && s.Situacao == Data.Entities.Sernit.SituacaoSernit.Agendada)
            .OrderByDescending(s => s.AtualizadoEm ?? s.CriadoEm)
            .Take(Maximo)
            .Select(s => new { s.Recurso, s.AgendadoParaTexto, s.UnidadeExecutora })
            .ToListAsync(ct);

        // ESUS de São Gonçalo (ADR-0063): PPI de exame de Maricá em SG. Diferente do SER/SERNIT, o
        // espelho tem data, hora e unidade exatas — então ENTRA na resposta (só o que é futuro).
        var hojeBr = FusoBrasilia.HojeEmBrasilia();
        var linhasEsusSg = await db.EsusSgSolicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == alvo.Value && s.ExcluidoEm == null
                && s.Situacao == Data.Entities.EsusSg.SituacaoEsusSg.Agendada
                && s.DataAgendada != null && s.DataAgendada >= hojeBr)
            .OrderBy(s => s.DataAgendada)
            .Take(Maximo)
            .Select(s => new { s.Recurso, s.DataAgendada, s.DataHoraAgendadaTexto, s.UnidadeExecutora })
            .ToListAsync(ct);

        if (futuros.Count == 0 && linhasSer.Count == 0 && linhasSernit.Count == 0 && linhasEsusSg.Count == 0
            && cancelados.Count == 0)
            return new(false,
                "NÃO localizei agendamento futuro NO NOSSO SISTEMA — o que NÃO quer dizer que não exista: "
                + "marcação feita agora pela equipe ou pela regulação pode ainda não ter chegado aqui. "
                + "NUNCA diga que a pessoa não tem nada agendado. E se um ATENDENTE ou a Secretaria já "
                + "anunciou um agendamento nesta conversa, ELE VALE: reconheça-o e trabalhe a partir "
                + "dele. Diga que não conseguiu localizar por aqui e peça para a pessoa conferir a guia "
                + "no posto onde é atendida. Encaminhe para atendimento humano SÓ se estiver dentro do "
                + "horário — fora dele, não prometa atendente: oriente a retornar no horário de "
                + "atendimento.");

        var linhas = futuros.Select((f, i) =>
        {
            var quando = f.DataAgendada is { } d ? FusoBrasilia.ParaExibicao(d).ToString("dd/MM/yyyy 'às' HH:mm") : "sem data";
            var onde = campanhas[i] is { } camp
                ? $" — {camp.LocalNome} ({camp.LocalEndereco}; atendimento neste local, mesmo que a guia indique outro)"
                : string.IsNullOrWhiteSpace(f.Unidade) ? string.Empty : $" — {f.Unidade}";
            var conf = f.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada ? " (já confirmado)" : string.Empty;
            return $"- {f.Procedimento ?? "atendimento"}: {quando}{onde}{conf}";
        }).ToList();

        linhas.AddRange(linhasEsusSg.Select(e =>
        {
            var quando = DescreverDataEsusSg(e.DataHoraAgendadaTexto, e.DataAgendada);
            var onde = string.IsNullOrWhiteSpace(e.UnidadeExecutora)
                ? " — em São Gonçalo"
                : $" — {e.UnidadeExecutora} (São Gonçalo)";
            return $"- {e.Recurso}: {quando}{onde} (marcado pela regulação de São Gonçalo)";
        }));

        // Regra PERENE (decisão do dono, 29/09/2026): perguntou "por que cancelou?", o robô lê o
        // motivo que a atendente registrou PARA o paciente — nunca inventa um, nunca manda
        // "perguntar no posto" quando a resposta está aqui.
        var linhasCancelados = cancelados.Select(c =>
        {
            var estava = c.DataAgendada is { } d
                ? $" que estava marcado para {FusoBrasilia.ParaExibicao(d):dd/MM/yyyy 'às' HH:mm}" : string.Empty;
            var porQue = string.IsNullOrWhiteSpace(c.MotivoCancelamentoParaPaciente)
                ? "sem motivo registrado para informar"
                : $"motivo registrado para informar ao paciente: \"{c.MotivoCancelamentoParaPaciente}\"";
            return $"- CANCELADO: {c.Procedimento ?? "atendimento"}{estava} — {porQue}";
        }).ToList();

        var resposta = string.Join("\n", linhas);
        if (linhasCancelados.Count > 0)
            resposta += (linhas.Count > 0 ? "\n\n" : string.Empty)
                + "Cancelados recentemente:\n" + string.Join("\n", linhasCancelados)
                + "\n\nSe a pessoa perguntar POR QUE foi cancelado, responda com o motivo registrado "
                + "acima, nas palavras registradas. Se estiver \"sem motivo registrado para informar\", "
                + "diga que a unidade de saúde pode detalhar — NUNCA invente um motivo. Cancelamento "
                + "não se desfaz por aqui: para remarcar, o posto onde a pessoa é atendida orienta.";

        return new(true,
            resposta
            + "\n\nInforme esses dados à pessoa. Lembre que a guia é retirada no posto onde ela é atendida. "
            + "NÃO invente nada além do que está acima.");
    }

    /// <summary>
    /// FASE 1: existe agendamento futuro para o CONTATO desta conversa? Não devolve detalhe nenhum
    /// — só o que o robô deve fazer em seguida. É o que garante "pedir dado somente SE TIVER
    /// informação para dar".
    /// </summary>
    private async Task<RoboComandoResultado> VerificarExistenciaAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        var candidatos = new List<Guid>();
        if (ctx.PacienteId is { } pid) candidatos.Add(pid);
        else candidatos.AddRange(
            (await PacientesDoNumeroAsync(ctx, ct)).Select(p => p.Id));

        if (candidatos.Count == 0)
            return new(false,
                "Este número não está vinculado a nenhum cadastro — não há como consultar agendamento "
                + "por aqui, e pedir CPF não adiantaria (a busca é pelo telefone). NÃO colete dado "
                + "nenhum. Oriente: o posto de saúde onde a pessoa é atendida tem a informação; "
                + "quando houver novidade, a Secretaria entra em contato por aqui. Encerre com cordialidade.");

        var agora = DateTime.UtcNow;
        var hojeBrasilia = FusoBrasilia.HojeEmBrasilia();
        var existe = await db.Solicitacoes.AsNoTracking().AnyAsync(
            s => candidatos.Contains(s.PacienteId)
                && s.ExcluidoEm == null && s.DataAgendada != null && s.DataAgendada >= agora
                && s.Status != StatusSolicitacao.Cancelada, ct)
            // Cancelamento recente também é informação a entregar: quem recebeu o aviso e pergunta
            // "por quê?" tem resposta registrada — sem isto a fase 1 mandava embora sem ela.
            || await db.Solicitacoes.AsNoTracking().AnyAsync(
                s => candidatos.Contains(s.PacienteId) && s.ExcluidoEm == null
                    && s.Status == StatusSolicitacao.Cancelada
                    && s.CanceladoEm != null && s.CanceladoEm >= agora.AddDays(-JanelaCanceladosDias), ct)
            || await db.SerSolicitacoes.AsNoTracking().AnyAsync(
                s => s.PacienteId != null && candidatos.Contains(s.PacienteId.Value)
                    && s.ExcluidoEm == null && s.Situacao == Data.Entities.Ser.SituacaoSer.Agendada, ct)
            || await db.SernitSolicitacoes.AsNoTracking().AnyAsync(
                s => s.PacienteId != null && candidatos.Contains(s.PacienteId.Value)
                    && s.ExcluidoEm == null && s.Situacao == Data.Entities.Sernit.SituacaoSernit.Agendada, ct)
            || await db.EsusSgSolicitacoes.AsNoTracking().AnyAsync(
                s => s.PacienteId != null && candidatos.Contains(s.PacienteId.Value)
                    && s.ExcluidoEm == null && s.Situacao == Data.Entities.EsusSg.SituacaoEsusSg.Agendada
                    && s.DataAgendada != null && s.DataAgendada >= hojeBrasilia, ct);

        if (!existe)
            return new(true,
                "NÃO há agendamento futuro NO NOSSO SISTEMA para este contato. NÃO peça CPF nem data "
                + "de nascimento — não há o que informar, e cobrar dados sem ter nada a entregar só "
                + "desgasta a pessoa. Explique que, assim que houver informação sobre o exame ou a "
                + "consulta que ela aguarda, a Secretaria entra em contato por este WhatsApp (o posto "
                + "onde ela é atendida também informa), e ENCERRE o atendimento com cordialidade. "
                + "NUNCA diga que o exame não foi ou não será marcado — apenas que ainda não chegou aqui.");

        return new(true,
            "HÁ agendamento futuro (ou cancelamento recente) registrado para este contato. NÃO revele "
            + "nada ainda: para informar, peça os *4 primeiros dígitos do CPF* do paciente (todos de "
            + "uma vez) e, depois que a pessoa responder, o mês e ano de nascimento — então chame esta "
            + "ferramenta de novo com os dados.");
    }

    /// <summary>Confere a identidade e devolve o paciente. Aceita o paciente da conversa e, quando o
    /// número não está amarrado, procura no cadastro por telefone.</summary>
    private async Task<Guid?> ResolverPacienteAsync(
        RoboComandoContexto ctx, string? cpf, int? mes, int? ano, CancellationToken ct)
    {
        if (ctx.PacienteId is { } id)
        {
            var p = await pacientes.ObterPorIdAsync(id, ct);
            return GateIdentidade.CpfInicioConfere(p.Cpf, cpf)
                && GateIdentidade.NascimentoMesAnoConfere(p.DataNascimento, mes, ano)
                ? p.Id : null;
        }

        foreach (var p in await PacientesDoNumeroAsync(ctx, ct))
        {
            if (GateIdentidade.CpfInicioConfere(p.Cpf, cpf)
                && GateIdentidade.NascimentoMesAnoConfere(p.DataNascimento, mes, ano))
                return p.Id;
        }
        return null;
    }

    /// <summary>Pacientes deste número, SEM os que tiveram o contato negado nele (LGPD).</summary>
    private async Task<List<Pacientes.Dtos.PacienteListItemDto>> PacientesDoNumeroAsync(
        RoboComandoContexto ctx, CancellationToken ct)
    {
        var lista = new List<Pacientes.Dtos.PacienteListItemDto>();
        foreach (var p in await pacientes.ListarPorTelefoneAsync(ctx.TelefoneCanonical, ct))
            if (!await contatosNegados.BloqueadoAsync(ctx.TelefoneCanonical, p.Id, ct))
                lista.Add(p);
        return lista;
    }

    /// <summary>"06/10/2026 13:15:00" (hora local de Brasília, como o ESUS formata) → "06/10/2026 às 13:15".</summary>
    private static string DescreverDataEsusSg(string? dataHoraTexto, DateOnly? data)
    {
        if (DateTime.TryParseExact(dataHoraTexto?.Trim(), ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm"],
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dh))
        {
            return dh.ToString("dd/MM/yyyy 'às' HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
        return data is { } d ? d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : "sem data";
    }
}
