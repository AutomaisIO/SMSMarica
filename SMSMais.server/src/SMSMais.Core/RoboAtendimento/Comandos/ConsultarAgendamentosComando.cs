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
/// </summary>
public sealed class ConsultarAgendamentosComando(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    PendenciasCadastro.IContatoNegadoService contatosNegados) : IRoboComando
{
    private const int Maximo = 5;

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
                s.StatusConfirmacao,
            })
            .ToListAsync(ct);

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

        if (futuros.Count == 0 && linhasSer.Count == 0 && linhasSernit.Count == 0)
            return new(false,
                "NÃO localizei agendamento futuro NO NOSSO SISTEMA — o que NÃO quer dizer que não exista: "
                + "marcação feita agora pela equipe ou pela regulação pode ainda não ter chegado aqui. "
                + "NUNCA diga que a pessoa não tem nada agendado. E se um ATENDENTE ou a Secretaria já "
                + "anunciou um agendamento nesta conversa, ELE VALE: reconheça-o e trabalhe a partir "
                + "dele. Diga que não conseguiu localizar por aqui e peça para a pessoa conferir a guia "
                + "no posto onde é atendida. Encaminhe para atendimento humano SÓ se estiver dentro do "
                + "horário — fora dele, não prometa atendente: oriente a retornar no horário de "
                + "atendimento.");

        var linhas = futuros.Select(f =>
        {
            var quando = f.DataAgendada is { } d ? FusoBrasilia.ParaExibicao(d).ToString("dd/MM/yyyy 'às' HH:mm") : "sem data";
            var onde = string.IsNullOrWhiteSpace(f.Unidade) ? string.Empty : $" — {f.Unidade}";
            var conf = f.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada ? " (já confirmado)" : string.Empty;
            return $"- {f.Procedimento ?? "atendimento"}: {quando}{onde}{conf}";
        });

        return new(true,
            string.Join("\n", linhas)
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
        var existe = await db.Solicitacoes.AsNoTracking().AnyAsync(
            s => candidatos.Contains(s.PacienteId)
                && s.ExcluidoEm == null && s.DataAgendada != null && s.DataAgendada >= agora, ct)
            || await db.SerSolicitacoes.AsNoTracking().AnyAsync(
                s => s.PacienteId != null && candidatos.Contains(s.PacienteId.Value)
                    && s.ExcluidoEm == null && s.Situacao == Data.Entities.Ser.SituacaoSer.Agendada, ct)
            || await db.SernitSolicitacoes.AsNoTracking().AnyAsync(
                s => s.PacienteId != null && candidatos.Contains(s.PacienteId.Value)
                    && s.ExcluidoEm == null && s.Situacao == Data.Entities.Sernit.SituacaoSernit.Agendada, ct);

        if (!existe)
            return new(true,
                "NÃO há agendamento futuro NO NOSSO SISTEMA para este contato. NÃO peça CPF nem data "
                + "de nascimento — não há o que informar, e cobrar dados sem ter nada a entregar só "
                + "desgasta a pessoa. Explique que, assim que houver informação sobre o exame ou a "
                + "consulta que ela aguarda, a Secretaria entra em contato por este WhatsApp (o posto "
                + "onde ela é atendida também informa), e ENCERRE o atendimento com cordialidade. "
                + "NUNCA diga que o exame não foi ou não será marcado — apenas que ainda não chegou aqui.");

        return new(true,
            "HÁ agendamento futuro registrado para este contato. NÃO revele nada ainda: para informar, "
            + "peça os *4 primeiros dígitos do CPF* do paciente (todos de uma vez) e, depois que a "
            + "pessoa responder, o mês e ano de nascimento — então chame esta ferramenta de novo com "
            + "os dados.");
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
}
