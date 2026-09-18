using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.SisregWeb.Comum;
using SMSMais.Core.Integracoes.SisregWeb.Ofertas.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Ofertas;

/// <summary>
/// <b>O que abriu</b> no SISREG — a leitura de oportunidade do dado que já coletamos.
///
/// <para>São duas coisas distintas, com urgências distintas, e o operador precisa das duas na
/// mesma tela:</para>
/// <list type="number">
///   <item><b>Agenda nova</b>: um bloco de escala que passou a existir ("abriu espirometria no CDT,
///   32 vagas"). Vem de <c>sisreg_escala</c>, pela data em que NÓS a vimos pela primeira vez
///   (<c>CriadoEm</c>) — não há coluna do SISREG dizendo "nasci agora".</item>
///   <item><b>Vaga liberada</b>: um agendamento que sumiu do export porque alguém cancelou lá.
///   Vem de <c>sisreg_alteracao_agenda</c> tipo <see cref="TipoAlteracaoAgenda.Ausente"/> — o
///   MESMO registro que a fila de alterações mostra como "confirme o cancelamento". É o mesmo
///   fato lido do outro lado: para quem perdeu, é cancelamento; para quem espera, é vaga.</item>
/// </list>
///
/// <para><b>Por que a espera entra aqui.</b> "4 vagas de ecocardiograma" é burocracia; "4 vagas
/// numa fila que espera 466 dias" muda o que a pessoa faz agora. A espera é o que ordena a tela.
/// <b>Ressalva que precisa aparecer na interface</b>: é a espera de quem JÁ foi atendido
/// (<c>data_agendada − data_solicitacao</c>), não a de quem está esperando. Serve para PRIORIZAR
/// entre procedimentos, não para prometer prazo.</para>
///
/// <para><b>Vigência não é data de vaga.</b> A reguladora apontou em 10/09/2026: a tela mostrava o
/// eletrocardiograma "de 01/07/25 a 31/12/26" e no SISREG a primeira vaga era em novembro. A
/// vigência é a validade do bloco semanal; quando dá para marcar sai de
/// <see cref="DatasDaOfertaAsync"/>, dia a dia.</para>
///
/// <para><b>Somente leitura.</b> Nada aqui escreve, nada aqui fala com o SISREG.</para>
/// </summary>
public interface IOfertasSisregService
{
    /// <param name="dias">Janela de "novidade" das agendas: quantos dias atrás olhar o
    /// <c>CriadoEm</c> da escala.</param>
    Task<OfertasSisregDto> ListarAsync(int dias, CancellationToken cancellationToken = default);

    /// <summary>
    /// As datas do procedimento por unidade executante: o que o SISREG mostra ao autorizar
    /// (unidades que executam → dias com vaga), deduzido de escala − agendados.
    /// </summary>
    Task<DatasDaOfertaDto> DatasDaOfertaAsync(
        string procedimentoCodigo, int dias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quem ocupa as vagas de UM dia da oferta: os agendamentos que o cartão do dia descontou
    /// ("3 de 4 livres"), com a mesma régua de <see cref="DatasDaOfertaAsync"/>.
    /// </summary>
    /// <param name="agendaLocal">Qual das agendas da unidade (numa unidade mista, a local e a
    /// regulada são cartões diferentes).</param>
    Task<OcupacaoDoDiaDto> OcupacaoDoDiaAsync(
        string procedimentoCodigo, Guid unidadeId, DateOnly data, bool agendaLocal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quem está esperando por este procedimento — a lista que a oferta destrava.
    /// </summary>
    /// <param name="ordenar">
    /// <c>espera</c> (padrão, mais antigo primeiro), <c>risco</c>, <c>idade</c> ou <c>nome</c>.
    /// </param>
    /// <param name="procedimentoCodigo">Com ele, entra na conta quem pediu o grupo (oferta de item)
    /// ou qualquer item do grupo (oferta de grupo). Sem ele, só o nome exato.</param>
    Task<FilaDaOfertaDto> FilaDaOfertaAsync(
        string procedimentoNome, string? ordenar, int limite, int pulo,
        string? procedimentoCodigo = null,
        CancellationToken cancellationToken = default);
}

/// <param name="pacientes">Nome/idade de quem ocupa a vaga, no hub FHIR. Opcional: sem ele (testes)
/// a ocupação sai sem nome — nunca quebra a tela por o hub estar fora.</param>
public sealed class OfertasSisregService(
    SmsMaisDbContext db, SMSMais.Core.Pacientes.Fhir.IPacienteResolver? pacientes = null) : IOfertasSisregService
{
    /// <summary>
    /// Espera acima da qual a oferta é destacada. Seis meses não é um número clínico — é o ponto a
    /// partir do qual a fila deixou de ser "demora" e virou outra coisa.
    /// </summary>
    private const int DiasEsperaUrgente = 180;

    /// <summary>
    /// Uma vaga que vaga para daqui a menos disto morre se ninguém agir hoje. É o que separa
    /// "aproveitável" de "registro histórico".
    /// </summary>
    private const int DiasVagaPerecivel = 7;

    /// <summary>
    /// Escala com mais que isto de vida e nenhum agendamento futuro é suspeita. Uma agenda aberta
    /// ontem ainda não teve tempo de receber marcação; uma de meses, sem nenhuma, não está sendo
    /// ofertada — foi o caso do ECG do CDT.
    /// </summary>
    private const int DiasParaDesconfiarDeAgendaVazia = 30;

    /// <summary>Horizonte das vagas livres do cartão — o mesmo das datas mostradas no clique.</summary>
    private const int HorizonteDasVagasLivres = 120;

    public async Task<OfertasSisregDto> ListarAsync(int dias, CancellationToken cancellationToken = default)
    {
        var janela = Math.Clamp(dias, 1, 90);
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var desde = DateTime.UtcNow.AddDays(-janela);

        var agendas = await AgendasNovasAsync(desde, hoje, cancellationToken);
        var vagas = await VagasLiberadasAsync(hoje, cancellationToken);

        // A espera é calculada UMA vez, só para os procedimentos que aparecem na tela. Rodar o
        // percentil sobre a base inteira (1M linhas) para depois jogar 99% fora seria caro à toa.
        var codigos = agendas.Select(a => a.ProcedimentoCodigo)
            .Concat(vagas.Select(v => v.ProcedimentoCodigo))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var espera = await EsperaPorProcedimentoAsync(codigos, cancellationToken);

        // O número do cartão é o que a regulação AINDA PODE MARCAR no procedimento — não o tamanho do
        // bloco que abriu. Em 12/09/2026 o ECO adulto aparecia com "4 vagas/semana" (o bloco novo do
        // Ernesto) e havia ~300 livres na rede. Mesma conta das datas do clique, uma vez por
        // procedimento na tela.
        var datas = new Dictionary<string, DatasDaOfertaDto>(StringComparer.Ordinal);
        foreach (var codigo in agendas.Select(a => a.ProcedimentoCodigo).Distinct(StringComparer.Ordinal))
        {
            datas[codigo] = await DatasDaOfertaAsync(codigo, HorizonteDasVagasLivres, cancellationToken);
        }

        // Quantos ESPERAM agora, por cartão — a mesma régua do "Quem espera" do clique, em lote.
        var naFila = await ContarNaFilaAsync(
            [.. agendas.Select(a => ((string?)a.ProcedimentoCodigo, a.ProcedimentoNome)),
             .. vagas.Where(v => v.ProcedimentoNome is not null)
                     .Select(v => (v.ProcedimentoCodigo, v.ProcedimentoNome!))],
            cancellationToken);

        return new OfertasSisregDto(
            [.. agendas.Select(a => ComVagasLivres(a, datas) with
                {
                    EsperaMedianaDias = Espera(espera, a.ProcedimentoCodigo),
                    NaFila = naFila.GetValueOrDefault(((string?)a.ProcedimentoCodigo, a.ProcedimentoNome)),
                })
                .OrderByDescending(a => a.EsperaMedianaDias ?? -1)
                .ThenByDescending(a => a.VagasLivresRegulacao ?? a.Vagas)],
            [.. vagas.Select(v => v with
                {
                    EsperaMedianaDias = Espera(espera, v.ProcedimentoCodigo),
                    NaFila = v.ProcedimentoNome is null
                        ? null
                        : naFila.GetValueOrDefault((v.ProcedimentoCodigo, v.ProcedimentoNome)),
                })
                .OrderBy(v => v.DataAgendada)],
            janela,
            DiasEsperaUrgente,
            DiasVagaPerecivel);
    }

    public async Task<DatasDaOfertaDto> DatasDaOfertaAsync(
        string procedimentoCodigo, int dias, CancellationToken cancellationToken = default)
    {
        var codigo = (procedimentoCodigo ?? string.Empty).Trim();
        if (codigo.Length == 0)
        {
            throw new Common.Excecoes.ValidacaoException(
                "datas.procedimento_obrigatorio", "Informe o procedimento para ver as datas.");
        }

        var horizonte = Math.Clamp(dias, 7, 180);
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var ate = hoje.AddDays(horizonte);
        var grupo = FamiliaProcedimentoSisreg.GrupoDoCodigo(codigo);

        // Item casa com a própria escala E com a do grupo dele: a vaga de "GRUPO - ULTRASONOGRAFIA"
        // é onde uma transvaginal é marcada. Grupo casa só consigo mesmo.
        var escalas = await db.SisregEscalas.AsNoTracking()
            .Where(e => (e.ProcedimentoCodigo == codigo || e.ProcedimentoCodigo == grupo)
                && e.Status == StatusEscalaSisreg.Ativa
                && !e.Ausente
                && e.VigenciaFim >= hoje
                && e.VigenciaInicio <= ate)
            .Select(e => new EscalaDaOferta(
                e.UnidadeId, e.Unidade!.Nome, e.ProcedimentoCodigo, e.ProcedimentoNome,
                e.ProfissionalNome, e.DiaSemana, e.HoraInicio, e.HoraFim,
                e.VigenciaInicio, e.VigenciaFim, e.VagasPrimeiraVez, e.VagasReserva, e.VagasTotal, e.AgendaLocal))
            .ToListAsync(cancellationToken);

        var nome = escalas.FirstOrDefault(e => e.ProcedimentoCodigo == codigo)?.ProcedimentoNome
                   ?? escalas.FirstOrDefault()?.ProcedimentoNome;

        if (escalas.Count == 0) return new DatasDaOfertaDto(codigo, nome, hoje, ate, []);

        // Ocupação: busca pela família (mesmo prefixo) e decide por unidade o que conta — em
        // escala de grupo, qualquer item do grupo ocupa a vaga; em escala de item, só o item.
        var unidadeIds = escalas.Select(e => e.UnidadeId).Distinct().ToList();
        var prefixo = codigo.Length >= 4 ? codigo[..4] : codigo;
        var inicioUtc = FusoBrasilia.InicioDoDiaAtualEmUtc();
        var fimUtc = FusoBrasilia.DeBrasiliaParaUtc(ate.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var agendamentos = await db.Solicitacoes.AsNoTracking()
            .Where(s => unidadeIds.Contains(s.UnidadeExecutanteId)
                && s.ExcluidoEm == null
                && s.CanceladoEm == null
                && s.DataAgendada >= inicioUtc
                && s.DataAgendada < fimUtc
                && s.ProcedimentoCodigoSisreg != null
                && s.ProcedimentoCodigoSisreg.StartsWith(prefixo))
            .Select(s => new { s.UnidadeExecutanteId, s.ProcedimentoCodigoSisreg, s.DataAgendada })
            .ToListAsync(cancellationToken);

        // Local × regulada é decidido POR ESCALA, não por unidade. Em 10/09/2026 o ECG do Bairro da
        // Amizade era local em set/out/dez e regulado só em novembro — o regulador só enxerga
        // novembro, e tratar a unidade inteira como "regulada" anunciava setembro. 63 pares
        // unidade × procedimento da rede são mistos assim. Unidade mista aparece duas vezes.
        var unidades = new List<UnidadeDaOfertaDto>();
        foreach (var daUnidade in escalas.GroupBy(e => (e.UnidadeId, e.AgendaLocal)))
        {
            var porFamilia = daUnidade.Any(e => FamiliaProcedimentoSisreg.EhCodigoDeGrupo(e.ProcedimentoCodigo));

            var ocupacao = agendamentos
                .Where(a => a.UnidadeExecutanteId == daUnidade.Key.UnidadeId
                    && (porFamilia || a.ProcedimentoCodigoSisreg == codigo))
                .GroupBy(a => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(a.DataAgendada!.Value)))
                .ToDictionary(g => g.Key, g => g.Count());

            var diasDaUnidade = ExpandirDias(daUnidade.ToList(), ocupacao, hoje, ate);

            // Só os agendamentos que caem em dia DESTA agenda: numa unidade mista, a marcação do
            // dia local não prova que a agenda regulada está sendo ofertada.
            var agendadosFuturos = diasDaUnidade.Sum(d => d.Agendados);

            var semAgendamentoFuturo = agendadosFuturos == 0
                && diasDaUnidade.Count > 0
                && daUnidade.Min(e => e.VigenciaInicio) <= hoje.AddDays(-DiasParaDesconfiarDeAgendaVazia);

            unidades.Add(new UnidadeDaOfertaDto(
                daUnidade.Key.UnidadeId,
                daUnidade.First().UnidadeNome,
                daUnidade.Key.AgendaLocal,
                diasDaUnidade.FirstOrDefault(d => d.Livres > 0)?.Data,
                diasDaUnidade.Sum(d => d.Livres),
                agendadosFuturos,
                semAgendamentoFuturo,
                diasDaUnidade));
        }

        // Regulada primeiro (é o que o regulador pode usar), a suspeita depois da confiável, e
        // dentro disso quem tem vaga mais cedo.
        return new DatasDaOfertaDto(
            codigo,
            nome,
            hoje,
            ate,
            [.. unidades
                .OrderBy(u => u.AgendaLocal)
                .ThenBy(u => u.SemAgendamentoFuturo)
                .ThenBy(u => u.PrimeiraVagaLivre ?? DateOnly.MaxValue)
                .ThenBy(u => u.UnidadeNome, StringComparer.Ordinal)]);
    }

    public async Task<OcupacaoDoDiaDto> OcupacaoDoDiaAsync(
        string procedimentoCodigo, Guid unidadeId, DateOnly data, bool agendaLocal,
        CancellationToken cancellationToken = default)
    {
        var codigo = (procedimentoCodigo ?? string.Empty).Trim();
        if (codigo.Length == 0)
        {
            throw new Common.Excecoes.ValidacaoException(
                "ocupacao.procedimento_obrigatorio", "Informe o procedimento para ver quem ocupa a vaga.");
        }

        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var grupo = FamiliaProcedimentoSisreg.GrupoDoCodigo(codigo);

        // As mesmas escalas que formaram o cartão: esta unidade, este tipo de agenda, vigentes.
        var escalas = await db.SisregEscalas.AsNoTracking()
            .Where(e => e.UnidadeId == unidadeId
                && e.AgendaLocal == agendaLocal
                && (e.ProcedimentoCodigo == codigo || e.ProcedimentoCodigo == grupo)
                && e.Status == StatusEscalaSisreg.Ativa
                && !e.Ausente
                && e.VigenciaFim >= hoje)
            .Select(e => new EscalaDaOferta(
                e.UnidadeId, e.Unidade!.Nome, e.ProcedimentoCodigo, e.ProcedimentoNome,
                e.ProfissionalNome, e.DiaSemana, e.HoraInicio, e.HoraFim,
                e.VigenciaInicio, e.VigenciaFim, e.VagasPrimeiraVez, e.VagasReserva, e.VagasTotal, e.AgendaLocal))
            .ToListAsync(cancellationToken);

        // Mesma régua de DatasDaOfertaAsync: escala de grupo é ocupada por qualquer item da família;
        // escala de item, só pelo item.
        var porFamilia = escalas.Any(e => FamiliaProcedimentoSisreg.EhCodigoDeGrupo(e.ProcedimentoCodigo));
        var prefixo = codigo.Length >= 4 ? codigo[..4] : codigo;
        var inicioUtc = FusoBrasilia.DeBrasiliaParaUtc(data.ToDateTime(TimeOnly.MinValue));
        var fimUtc = FusoBrasilia.DeBrasiliaParaUtc(data.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var agendados = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.UnidadeExecutanteId == unidadeId
                && s.ExcluidoEm == null
                && s.CanceladoEm == null
                && s.DataAgendada >= inicioUtc
                && s.DataAgendada < fimUtc
                && s.ProcedimentoCodigoSisreg != null
                && s.ProcedimentoCodigoSisreg.StartsWith(prefixo)
                && (porFamilia || s.ProcedimentoCodigoSisreg == codigo))
            .OrderBy(s => s.DataAgendada)
            .Select(s => new
            {
                s.Id,
                s.PacienteId,
                s.CodigoSolicitacao,
                DataAgendada = s.DataAgendada!.Value,
                s.ProcedimentoTexto,
                s.ProfissionalExecutanteNome,
                UnidadeSolicitante = s.UnidadeSolicitante != null ? s.UnidadeSolicitante.Nome : null,
                s.StatusConfirmacao,
                s.Categoria,
            })
            .ToListAsync(cancellationToken);

        var ids = agendados.Select(a => a.PacienteId).Where(id => id != Guid.Empty).Distinct().ToList();
        IReadOnlyDictionary<Guid, SMSMais.Core.Pacientes.Fhir.PacienteResumo> nomes =
            new Dictionary<Guid, SMSMais.Core.Pacientes.Fhir.PacienteResumo>();
        if (pacientes is not null && ids.Count > 0)
        {
            try
            {
                nomes = await pacientes.ResolverManyAsync(ids, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Hub fora: a lista sai sem nome (com o código do SISREG) em vez de a tela quebrar.
            }
        }

        var blocos = escalas
            .Where(e => e.DiaSemana == data.DayOfWeek && e.VigenciaInicio <= data && e.VigenciaFim >= data)
            .ToList();
        var vagasDoDia = blocos.Sum(b => b.VagasTotal);
        var daRegulacao = blocos.Sum(b => b.VagasPrimeiraVez + b.VagasReserva);

        var ocupantes = agendados.Select(a =>
        {
            var p = nomes.GetValueOrDefault(a.PacienteId);
            int? idade = p?.DataNascimento is { } nasc
                ? hoje.Year - nasc.Year - (hoje < nasc.AddYears(hoje.Year - nasc.Year) ? 1 : 0)
                : null;
            return new OcupanteDaVagaDto(
                a.Id,
                a.CodigoSolicitacao,
                TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(a.DataAgendada)),
                p?.Nome,
                idade,
                p?.Cns,
                a.ProcedimentoTexto,
                a.ProfissionalExecutanteNome,
                a.UnidadeSolicitante,
                a.StatusConfirmacao,
                a.Categoria);
        }).ToList();

        return new OcupacaoDoDiaDto(
            codigo,
            unidadeId,
            escalas.FirstOrDefault()?.UnidadeNome,
            data,
            blocos.Count > 0 ? blocos.Min(b => b.HoraInicio) : null,
            blocos.Count > 0 ? blocos.Max(b => b.HoraFim) : null,
            daRegulacao,
            Math.Clamp(vagasDoDia - ocupantes.Count, 0, daRegulacao),
            ocupantes);
    }

    /// <summary>
    /// Um dia por ocorrência da escala no horizonte. Vagas são as <b>da regulação</b>: primeira vez
    /// <b>mais reserva</b>. Retorno fica com a unidade. Livres é o que sobra da agenda do dia
    /// inteira, limitado a elas — um agendamento de retorno ocupa a vaga de retorno antes.
    ///
    /// <para><b>A reserva conta.</b> Até 12/09/2026 só a primeira vez contava, e o ECO da DIMAGEM
    /// (e do CDT, e do Ernesto) declara as vagas da regulação TODAS como reserva, com zero de
    /// primeira vez: a tela dizia "lotado" em todo dia, e as vagas de 12 a 26/11 que a reguladora
    /// via no SISREG não apareciam.</para>
    /// </summary>
    private static List<DiaDaOfertaDto> ExpandirDias(
        IReadOnlyList<EscalaDaOferta> escalas, IReadOnlyDictionary<DateOnly, int> ocupacao,
        DateOnly de, DateOnly ate)
    {
        var dias = new List<DiaDaOfertaDto>();
        for (var d = de; d <= ate; d = d.AddDays(1))
        {
            var blocos = escalas
                .Where(e => e.DiaSemana == d.DayOfWeek && e.VigenciaInicio <= d && e.VigenciaFim >= d)
                .ToList();
            if (blocos.Count == 0) continue;

            var vagasDoDia = blocos.Sum(b => b.VagasTotal);
            var daRegulacao = blocos.Sum(b => b.VagasPrimeiraVez + b.VagasReserva);
            if (vagasDoDia == 0) continue;

            var agendados = ocupacao.GetValueOrDefault(d);
            dias.Add(new DiaDaOfertaDto(
                d,
                blocos.Min(b => b.HoraInicio),
                blocos.Max(b => b.HoraFim),
                daRegulacao,
                agendados,
                Math.Clamp(vagasDoDia - agendados, 0, daRegulacao),
                [.. blocos.Select(b => b.ProfissionalNome)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)]));
        }
        return dias;
    }

    public async Task<FilaDaOfertaDto> FilaDaOfertaAsync(
        string procedimentoNome, string? ordenar, int limite, int pulo,
        string? procedimentoCodigo = null,
        CancellationToken cancellationToken = default)
    {
        var nome = (procedimentoNome ?? string.Empty).Trim();
        if (nome.Length == 0)
        {
            throw new Common.Excecoes.ValidacaoException(
                "fila.procedimento_obrigatorio", "Informe o procedimento para listar a fila.");
        }

        var pagina = Math.Clamp(limite, 1, 500);
        var salto = Math.Max(0, pulo);
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));

        // Casa pelo NOME porque o SISREG não manda código do procedimento na tela da fila — é a
        // régua da casa. Comparação exata, sem `Contains`: "CONSULTA EM CARDIOLOGIA" não pode
        // arrastar "CONSULTA EM CARDIOLOGIA - PEDIATRIA", que é outra fila com outra espera. O que
        // o código acrescenta é o outro lado do grupo, também por nome exato.
        var nomes = await FamiliaProcedimentoSisreg.NomesDaFamiliaAsync(db, nome, procedimentoCodigo?.Trim(), cancellationToken);

        var baseQuery = db.SisregFilaPendentes.AsNoTracking()
            .Where(f => f.SaiuEm == null && f.ProcedimentoNome != null && nomes.Contains(f.ProcedimentoNome));

        var total = await baseQuery.CountAsync(cancellationToken);

        var porRisco = await baseQuery
            .GroupBy(f => f.Risco)
            .Select(g => new { Risco = g.Key, Qtd = g.Count() })
            .ToListAsync(cancellationToken);

        var esperas = await baseQuery
            .Where(f => f.DataSolicitacao != null)
            .Select(f => f.DataSolicitacao!.Value)
            .ToListAsync(cancellationToken);

        var dias = esperas.Select(d => hoje.DayNumber - d.DayNumber).Where(x => x >= 0).Order().ToList();

        var ordenada = (ordenar?.Trim().ToLowerInvariant()) switch
        {
            // Risco primeiro, e dentro do mesmo risco quem espera há mais tempo. Nulo por último:
            // "não classificado" não pode passar na frente de um vermelho.
            "risco" => baseQuery.OrderBy(f => f.Risco == null).ThenBy(f => f.Risco)
                .ThenBy(f => f.DataSolicitacao),
            "idade" => baseQuery.OrderByDescending(f => f.IdadeAnos ?? -1).ThenBy(f => f.DataSolicitacao),
            "nome" => baseQuery.OrderBy(f => f.PacienteNome).ThenBy(f => f.DataSolicitacao),
            // Padrão: quem chegou primeiro. É o critério mais defensável numa fila pública.
            _ => baseQuery.OrderBy(f => f.DataSolicitacao == null).ThenBy(f => f.DataSolicitacao),
        };

        var linhas = await ordenada.Skip(salto).Take(pagina)
            .Select(f => new
            {
                f.CodigoSolicitacao, f.DataSolicitacao, f.Risco, f.PacienteNome,
                f.IdadeAnos, f.DataNascimento, f.Cns, f.Telefone, f.UnidadeSolicitante, f.CidCodigo,
            })
            .ToListAsync(cancellationToken);

        return new FilaDaOfertaDto(
            nome,
            total,
            dias.Count > 0 ? dias[dias.Count / 2] : null,
            dias.Count > 0 ? dias[^1] : null,
            porRisco.ToDictionary(x => x.Risco?.ToString() ?? "sem", x => x.Qtd),
            [.. linhas.Select(l => new PessoaNaFilaDto(
                l.CodigoSolicitacao,
                l.DataSolicitacao,
                l.DataSolicitacao is { } d ? hoje.DayNumber - d.DayNumber : null,
                l.Risco,
                l.PacienteNome,
                l.IdadeAnos,
                l.DataNascimento,
                l.Cns,
                l.Telefone,
                l.UnidadeSolicitante,
                l.CidCodigo))],
            [.. nomes.Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Quantas pessoas esperam por cada procedimento dos cartões — a MESMA régua de
    /// <see cref="FamiliaProcedimentoSisreg.NomesDaFamiliaAsync"/> (nome exato + o outro lado do grupo), feita em lote.
    ///
    /// <para><b>Em lote porque a tela tem centenas de cartões.</b> Uma consulta agrupa a fila por
    /// nome; uma traz os nomes das escalas dos prefixos na tela; e só os GRUPOS pedem a busca cara
    /// em <c>solicitacao</c> (os itens pedidos de cada grupo), uma vez para todos.</para>
    /// </summary>
    private async Task<Dictionary<(string? Codigo, string Nome), int>> ContarNaFilaAsync(
        IReadOnlyCollection<(string? Codigo, string Nome)> chaves, CancellationToken ct)
    {
        var resultado = new Dictionary<(string? Codigo, string Nome), int>();
        if (chaves.Count == 0) return resultado;

        var porNome = await db.SisregFilaPendentes.AsNoTracking()
            .Where(f => f.SaiuEm == null && f.ProcedimentoNome != null)
            .GroupBy(f => f.ProcedimentoNome!)
            .Select(g => new { Nome = g.Key, Qtd = g.Count() })
            .ToDictionaryAsync(x => x.Nome, x => x.Qtd, StringComparer.Ordinal, ct);

        var codigos = chaves
            .Select(c => c.Codigo)
            .Where(c => c is not null && FamiliaProcedimentoSisreg.GrupoDoCodigo(c) is not null)
            .Select(c => c!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var prefixos = codigos.Select(c => c[..4]).Distinct(StringComparer.Ordinal).ToList();
        var prefixosDeGrupo = codigos.Where(FamiliaProcedimentoSisreg.EhCodigoDeGrupo).Select(c => c[..4])
            .Distinct(StringComparer.Ordinal).ToList();

        var escalas = new List<(string Codigo, string Nome)>();
        if (prefixos.Count > 0)
        {
            var lidas = await db.SisregEscalas.AsNoTracking()
                .Where(e => prefixos.Contains(e.ProcedimentoCodigo.Substring(0, 4)))
                .Select(e => new { e.ProcedimentoCodigo, e.ProcedimentoNome })
                .Distinct()
                .ToListAsync(ct);
            escalas.AddRange(lidas.Select(e => (e.ProcedimentoCodigo, e.ProcedimentoNome)));
        }

        var itensDeGrupo = new List<(string Codigo, string Nome)>();
        if (prefixosDeGrupo.Count > 0)
        {
            var lidos = await db.Solicitacoes.AsNoTracking()
                .Where(s => s.ProcedimentoCodigoSisreg != null
                    && s.ProcedimentoTexto != null
                    && prefixosDeGrupo.Contains(s.ProcedimentoCodigoSisreg.Substring(0, 4)))
                .Select(s => new { Codigo = s.ProcedimentoCodigoSisreg!, Nome = s.ProcedimentoTexto! })
                .Distinct()
                .ToListAsync(ct);
            itensDeGrupo.AddRange(lidos.Select(s => (s.Codigo, s.Nome)));
        }

        foreach (var chave in chaves.Distinct())
        {
            var nomes = new HashSet<string>(StringComparer.Ordinal) { chave.Nome };
            if (chave.Codigo is { } codigo && FamiliaProcedimentoSisreg.GrupoDoCodigo(codigo) is { } grupo)
            {
                var prefixo = codigo[..4];
                if (FamiliaProcedimentoSisreg.EhCodigoDeGrupo(codigo))
                {
                    nomes.UnionWith(escalas.Where(e => e.Codigo.StartsWith(prefixo, StringComparison.Ordinal)).Select(e => e.Nome));
                    nomes.UnionWith(itensDeGrupo.Where(i => i.Codigo.StartsWith(prefixo, StringComparison.Ordinal)).Select(i => i.Nome));
                }
                else
                {
                    nomes.UnionWith(escalas.Where(e => e.Codigo == grupo).Select(e => e.Nome));
                }
            }

            resultado[chave] = nomes.Sum(n => porNome.GetValueOrDefault(n));
        }

        return resultado;
    }

    /// <summary>
    /// Vagas livres do procedimento para o cartão: soma das unidades REGULADAS e confiáveis (fora a
    /// agenda local, que o regulador não vê, e a "vaga não confirmada", como o ECG do CDT), mais as
    /// livres da própria unidade do cartão.
    /// </summary>
    private static AgendaNovaDto ComVagasLivres(
        AgendaNovaDto a, IReadOnlyDictionary<string, DatasDaOfertaDto> datas)
    {
        if (!datas.TryGetValue(a.ProcedimentoCodigo, out var d)) return a;

        var confiaveis = d.Unidades.Where(u => !u.AgendaLocal && !u.SemAgendamentoFuturo).ToList();
        return a with
        {
            VagasLivresRegulacao = confiaveis.Sum(u => u.VagasLivres),
            PrimeiraVagaLivreRegulacao = confiaveis.Min(u => u.PrimeiraVagaLivre),
            UnidadesComVaga = confiaveis.Count(u => u.VagasLivres > 0),
            VagasLivresUnidade = d.Unidades
                .Where(u => u.UnidadeId == a.UnidadeId && u.AgendaLocal == a.AgendaLocal)
                .Sum(u => u.VagasLivres),
        };
    }

    private static int? Espera(IReadOnlyDictionary<string, int> mapa, string? codigo) =>
        codigo is not null && mapa.TryGetValue(codigo, out var d) ? d : null;

    /// <summary>
    /// Acima disto, uma execução do sincronismo não trouxe novidade: ela POVOOU a base. A primeira
    /// sincronização de todas criou 17.452 escalas de uma vez (04/09/2026) — chamar aquilo de
    /// "agenda nova" faria a tela anunciar 10.456 vagas que ninguém abriu, e o operador aprenderia
    /// no primeiro dia que este número é mentira.
    /// </summary>
    private const int EscalasNovasQueDenunciamCarga = 500;

    /// <summary>
    /// Blocos de escala vistos pela primeira vez dentro da janela, agrupados por unidade ×
    /// procedimento.
    ///
    /// <para><b>Agrupar é o ponto.</b> Uma agenda semanal nasce como várias linhas — uma por dia da
    /// semana, às vezes uma por profissional. As 32 vagas de espirometria do CDT são 7 escalas; sem
    /// agrupar, a tela mostraria 7 "novidades" para um fato só e o operador aprenderia a ignorá-la.</para>
    ///
    /// <para>Só entra escala <b>ativa, não ausente e ainda vigente</b>: bloco que já venceu não é
    /// oferta, é histórico.</para>
    /// </summary>
    private async Task<List<AgendaNovaDto>> AgendasNovasAsync(
        DateTime desde, DateOnly hoje, CancellationToken ct)
    {
        // Janelas de tempo das execuções que foram CARGA, não novidade. O que nasceu dentro delas
        // fica de fora: é o retrato inicial do SISREG, não oferta que apareceu.
        var cargas = await db.SisregEscalaSincronizacaoExecucoes.AsNoTracking()
            .Where(e => e.EscalasNovas >= EscalasNovasQueDenunciamCarga && e.IniciadoEm >= desde)
            .Select(e => new { e.IniciadoEm, e.FinalizadoEm })
            .ToListAsync(ct);

        // Busca CHAPADA e agrupa em memória, de propósito. Agrupar no banco parece mais barato mas
        // não compila: `Distinct()` sobre uma coleção dentro da projeção de um GroupBy não tem
        // tradução no EF ("Unable to translate a collection subquery in a projection") — e isso só
        // aparece em RUNTIME, com 500 na cara do operador. O conjunto aqui é pequeno por
        // construção (só escalas vistas nos últimos N dias: 32 numa janela de 5 dias, medido em
        // 09/09/2026), então trazer as linhas custa menos que a ginástica para o SQL aceitar.
        var linhas = await db.SisregEscalas.AsNoTracking()
            .Where(e => e.CriadoEm >= desde
                && e.Status == StatusEscalaSisreg.Ativa
                && !e.Ausente
                && e.VigenciaFim >= hoje)
            .Select(e => new
            {
                e.UnidadeId,
                UnidadeNome = e.Unidade!.Nome,
                e.ProcedimentoCodigo,
                e.ProcedimentoNome,
                e.CboDescricao,
                e.DiaSemana,
                e.VagasTotal,
                e.VigenciaInicio,
                e.VigenciaFim,
                e.CriadoEm,
                e.AgendaLocal,
            })
            .ToListAsync(ct);

        // Folga de 1 minuto de cada lado: `criado_em` é carimbado linha a linha durante a gravação,
        // então uma escala da carga pode cair alguns segundos depois do `FinalizadoEm` registrado.
        if (cargas.Count > 0)
        {
            linhas = [.. linhas.Where(l => !cargas.Any(c =>
                l.CriadoEm >= c.IniciadoEm.AddMinutes(-1)
                && l.CriadoEm <= (c.FinalizadoEm ?? c.IniciadoEm.AddHours(1)).AddMinutes(1)))];
        }

        return [.. linhas
            .GroupBy(e => new { e.UnidadeId, e.ProcedimentoCodigo })
            .Select(g => new AgendaNovaDto(
                g.Key.UnidadeId,
                g.First().UnidadeNome,
                g.Key.ProcedimentoCodigo,
                g.First().ProcedimentoNome,
                g.First().CboDescricao,
                g.Count(),
                g.Sum(e => e.VagasTotal),
                g.Min(e => e.VigenciaInicio),
                g.Max(e => e.VigenciaFim),
                // Dias da semana em que essa agenda abre — é o que diz "toda terça" vs "um dia só".
                [.. g.Select(e => (int)e.DiaSemana).Distinct().Order()],
                g.Min(e => e.CriadoEm),
                null,
                // Local só se TODOS os blocos forem: um bloco regulado já é vaga para a regulação.
                g.All(e => e.AgendaLocal)))];
    }

    /// <summary>
    /// Agendamentos que sumiram do SISREG e cuja data ainda não passou — as vagas que dá para
    /// reaproveitar.
    ///
    /// <para>Só <b>pendentes</b> (<c>TratadaEm == null</c>): tratada quer dizer que alguém já
    /// decidiu o que fazer, e continuar oferecendo o que já foi resolvido é como a fila de
    /// alterações vira ruído.</para>
    ///
    /// <para><b>Não afirmamos que a vaga está livre</b> — afirmamos que o SISREG parou de mostrar
    /// aquele agendamento. Confirmar é trabalho de gente, e a tela diz isso.</para>
    ///
    /// <para><b>E dizemos para quem ela volta.</b> Em agenda local a vaga volta para a própria
    /// unidade: as 4 "vagas de ECG" que a tela mostrava em 10/09/2026 eram todas de USF de agenda
    /// local, que o regulador nunca vê.</para>
    /// </summary>
    private async Task<List<VagaLiberadaDto>> VagasLiberadasAsync(DateOnly hoje, CancellationToken ct)
    {
        // Pelo HORÁRIO, não pelo dia: vaga das 08:00 não é mais vaga às 15:00 (pedido de 12/09/2026 —
        // a lista mostrava horário do dia que já tinha passado).
        var agoraUtc = DateTime.UtcNow;

        var vagas = await db.SisregAlteracoesAgenda.AsNoTracking()
            .Where(a => a.Tipo == TipoAlteracaoAgenda.Ausente && a.TratadaEm == null)
            .Join(db.Solicitacoes.AsNoTracking().Where(s => s.ExcluidoEm == null && s.DataAgendada > agoraUtc),
                a => a.SolicitacaoId, s => s.Id, (a, s) => new { a, s })
            .Select(x => new VagaLiberadaDto(
                x.a.Id,
                x.s.Id,
                x.s.UnidadeExecutanteId,
                x.s.UnidadeExecutante!.Nome,
                x.s.ProcedimentoCodigoSisreg,
                x.s.ProcedimentoTexto,
                x.s.DataAgendada!.Value,
                x.a.DetectadaEm,
                null,
                null,
                null))
            .ToListAsync(ct);

        if (vagas.Count == 0) return vagas;

        var unidades = vagas.Where(v => v.UnidadeId is not null).Select(v => v.UnidadeId!.Value).Distinct().ToList();
        var escalas = await db.SisregEscalas.AsNoTracking()
            .Where(e => unidades.Contains(e.UnidadeId)
                && e.Status == StatusEscalaSisreg.Ativa
                && !e.Ausente
                && e.VigenciaFim >= hoje)
            .Select(e => new { e.UnidadeId, e.ProcedimentoCodigo, e.AgendaLocal, e.DiaSemana, e.VigenciaInicio, e.VigenciaFim })
            .ToListAsync(ct);

        return [.. vagas.Select(v =>
        {
            if (v.UnidadeId is null || string.IsNullOrEmpty(v.ProcedimentoCodigo)) return v;
            var grupo = FamiliaProcedimentoSisreg.GrupoDoCodigo(v.ProcedimentoCodigo);
            var doProcedimento = escalas
                .Where(e => e.UnidadeId == v.UnidadeId
                    && (e.ProcedimentoCodigo == v.ProcedimentoCodigo || e.ProcedimentoCodigo == grupo))
                .ToList();

            // A escala que cobre o DIA da vaga decide: na mesma unidade o setembro pode ser local e
            // o novembro regulado. Sem escala no dia, cai para a unidade inteira.
            var dia = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(v.DataAgendada));
            var doDia = doProcedimento
                .Where(e => e.DiaSemana == dia.DayOfWeek && e.VigenciaInicio <= dia && e.VigenciaFim >= dia)
                .ToList();
            var decide = doDia.Count > 0 ? doDia : doProcedimento;

            return decide.Count == 0 ? v : v with { AgendaLocal = decide.All(e => e.AgendaLocal) };
        })];
    }

    /// <summary>
    /// Mediana de <c>data_agendada − data_solicitacao</c> por procedimento, sobre o que foi
    /// agendado neste ano.
    ///
    /// <para><b>O que este número é e o que não é.</b> É a espera de quem JÁ conseguiu data —
    /// enviesada para baixo por construção, porque quem nunca foi atendido não entra na conta.
    /// Serve para ordenar procedimentos entre si, não para prometer prazo a ninguém.</para>
    ///
    /// <para>Feito em SQL cru porque <c>percentile_disc</c> não tem tradução no LINQ. Sem SQL cru
    /// seria trazer as linhas para a memória — e são centenas de milhares.</para>
    /// </summary>
    private async Task<Dictionary<string, int>> EsperaPorProcedimentoAsync(
        IReadOnlyList<string> codigos, CancellationToken ct)
    {
        if (codigos.Count == 0) return [];

        // `DateOnly` e não `DateTime`: DateTime Unspecified quebra em runtime no Npgsql com coluna
        // de data — armadilha já paga uma vez neste projeto.
        var inicioAno = new DateOnly(FusoBrasilia.ParaExibicao(DateTime.UtcNow).Year, 1, 1);

        var linhas = await db.Database
            .SqlQuery<EsperaLinha>($"""
                SELECT s.procedimento_codigo_sisreg AS "Codigo",
                       percentile_disc(0.5) WITHIN GROUP (
                           ORDER BY (s.data_agendada::date - s.data_solicitacao::date))::int AS "Dias"
                FROM smsmarica.solicitacao s
                WHERE s.procedimento_codigo_sisreg = ANY({codigos})
                  AND s.excluido_em IS NULL
                  AND s.data_solicitacao IS NOT NULL
                  AND s.data_agendada >= {inicioAno}
                GROUP BY s.procedimento_codigo_sisreg
                """)
            .ToListAsync(ct);

        return linhas
            .Where(l => l.Codigo is not null && l.Dias is not null)
            .ToDictionary(l => l.Codigo!, l => l.Dias!.Value, StringComparer.Ordinal);
    }

    private sealed record EsperaLinha(string? Codigo, int? Dias);

    private sealed record EscalaDaOferta(
        Guid UnidadeId,
        string UnidadeNome,
        string ProcedimentoCodigo,
        string ProcedimentoNome,
        string ProfissionalNome,
        DayOfWeek DiaSemana,
        TimeOnly HoraInicio,
        TimeOnly HoraFim,
        DateOnly VigenciaInicio,
        DateOnly VigenciaFim,
        int VagasPrimeiraVez,
        int VagasReserva,
        int VagasTotal,
        bool AgendaLocal);
}
