using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Solicitacoes;

public sealed record CriarRegulacaoSolicitacaoRequest(
    FluxoRegulacao Fluxo,
    Guid ProcedimentoId,
    Guid PacienteId,
    /// <summary>Obrigatória no NAR: a unidade em cujo nome a solicitação entra no SISREG (D-9).</summary>
    Guid? UnidadeEmNomeDeId,
    SistemaRegulacao? SistemaDestino,
    string? Observacoes);

public sealed record AtualizarRegulacaoSolicitacaoRequest(
    SistemaRegulacao? SistemaDestino,
    JsonElement? Formulario,
    string? Observacoes);

public sealed record RegulacaoSolicitacaoDetalheDto(
    Guid Id,
    long NumeroLocal,
    FluxoRegulacao Fluxo,
    StatusRegulacao Status,
    string? StatusMotivo,
    Guid UnidadeSolicitanteId,
    Guid? UnidadeEmNomeDeId,
    Guid PacienteId,
    string PacienteNome,
    string? PacienteCpf,
    Guid ProcedimentoId,
    string ProcedimentoNome,
    SistemaRegulacao? SistemaDestino,
    Guid? FormularioVersaoId,
    JsonElement Formulario,
    string? NumeroExterno,
    string? Observacoes,
    DateTime CriadoEm);

/// <summary>Por que a solicitação ainda não pode ir para a fila.</summary>
public sealed record PendenciaEnvioDto(string Codigo, string Descricao);

/// <param name="SoMinhas">
/// Só as que este usuário abriu. Vale para o agente, que enxerga tudo e às vezes quer ver o
/// próprio trabalho.
/// </param>
public sealed record RegulacaoSolicitacaoFiltro(
    StatusRegulacao[]? Status = null,
    FluxoRegulacao? Fluxo = null,
    SistemaRegulacao? Sistema = null,
    Guid? ProcedimentoId = null,
    Guid? UnidadeSolicitanteId = null,
    Guid? AgenteId = null,
    string? Busca = null,
    bool SoMinhas = false,
    int Pagina = 1,
    int Tamanho = 25);

public sealed record RegulacaoSolicitacaoListaDto(
    Guid Id,
    long NumeroLocal,
    string? NumeroExterno,
    SistemaRegulacao? SistemaDestino,
    FluxoRegulacao Fluxo,
    StatusRegulacao Status,
    string PacienteNome,
    string? PacienteCpf,
    string Procedimento,
    Guid UnidadeSolicitanteId,
    string UnidadeSolicitante,
    string? UnidadeEmNomeDe,
    string? AgenteNome,
    DateTime CriadoEm,
    DateTime? AtualizadoEm);

public sealed record PaginaSolicitacoesRegulacaoDto(
    int Total, IReadOnlyList<RegulacaoSolicitacaoListaDto> Itens);

/// <param name="PorStatus">Contagem por status — alimenta as abas da fila e o badge da sidebar.</param>
/// <param name="VeTodasUnidades">Se este usuário está enxergando o município inteiro (módulo 48).</param>
public sealed record RegulacaoResumoFilaDto(
    IReadOnlyDictionary<StatusRegulacao, int> PorStatus, bool VeTodasUnidades);

public interface IRegulacaoSolicitacaoService
{
    Task<PaginaSolicitacoesRegulacaoDto> ListarAsync(
        RegulacaoSolicitacaoFiltro filtro, CancellationToken ct);

    /// <summary>Contagem por status, no escopo do usuário.</summary>
    Task<RegulacaoResumoFilaDto> ResumoAsync(CancellationToken ct);

    Task<RegulacaoSolicitacaoDetalheDto> CriarAsync(
        CriarRegulacaoSolicitacaoRequest req, CancellationToken ct);

    Task<RegulacaoSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct);

    Task<RegulacaoSolicitacaoDetalheDto> AtualizarAsync(
        Guid id, AtualizarRegulacaoSolicitacaoRequest req, CancellationToken ct);

    /// <summary>O que falta para sair do rascunho. Lista vazia = pode enviar.</summary>
    Task<IReadOnlyList<PendenciaEnvioDto>> PendenciasDeEnvioAsync(Guid id, CancellationToken ct);

    Task<RegulacaoSolicitacaoDetalheDto> EnviarParaFilaAsync(Guid id, CancellationToken ct);

    Task CancelarAsync(Guid id, string motivo, CancellationToken ct);

    /// <summary>A história do caso, em ordem. É o que a linha do tempo da tela mostra.</summary>
    Task<IReadOnlyList<RegulacaoEventoDto>> EventosAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Abertura e edição da solicitação até ela entrar na fila de pré-regulação (planos 02 e 04).
///
/// <para><b>Escopo por unidade é fail-closed.</b> A leitura passa por
/// <see cref="EscopoUnidade"/>: quem não tem vínculo não enxerga nada, em vez de enxergar tudo.
/// Ampliação para o agente regulador (módulo 48) é do incremento 3.</para>
///
/// <para><b>Nada aqui escreve em sistema de regulação</b> (D-11). "Enviar para a fila" põe a
/// solicitação em <see cref="StatusRegulacao.PendenteRegulacao"/> e para; a inclusão no SISREG
/// pelo próprio solicitante (D-8) depende do spike b e entra no incremento 7.</para>
/// </summary>
public sealed class RegulacaoSolicitacaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IRegulacaoFormularioService formularios,
    IRegulacaoExigenciaService exigencias,
    IRegulacaoConfiguracaoService configuracao,
    IRegulacaoProcedimentoBuscaService catalogo,
    IRegulacaoEventoService eventos,
    IRegulacaoEscopo escopoRegulacao,
    IPacientesService pacientes) : IRegulacaoSolicitacaoService
{
    private static readonly JsonElement ObjetoVazio = JsonDocument.Parse("{}").RootElement.Clone();

    public async Task<PaginaSolicitacoesRegulacaoDto> ListarAsync(
        RegulacaoSolicitacaoFiltro filtro, CancellationToken ct)
    {
        var consulta = await ConsultaNoEscopoAsync(ct);
        if (consulta is null) return new PaginaSolicitacoesRegulacaoDto(0, []);

        if (filtro.Status is { Length: > 0 } status) consulta = consulta.Where(s => status.Contains(s.Status));
        if (filtro.Fluxo is { } fluxo) consulta = consulta.Where(s => s.Fluxo == fluxo);
        if (filtro.Sistema is { } sistema) consulta = consulta.Where(s => s.SistemaDestino == sistema);
        if (filtro.ProcedimentoId is { } proc) consulta = consulta.Where(s => s.ProcedimentoId == proc);
        if (filtro.UnidadeSolicitanteId is { } unid) consulta = consulta.Where(s => s.UnidadeSolicitanteId == unid);
        if (filtro.AgenteId is { } agente) consulta = consulta.Where(s => s.AgenteResponsavelId == agente);
        if (filtro.SoMinhas && usuarioAtual.UsuarioId is { } eu)
        {
            consulta = consulta.Where(s => s.CriadoPorUsuarioId == eu);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var termo = filtro.Busca.Trim();
            var digitos = new string([.. termo.Where(char.IsDigit)]);

            // O número local é buscado por IGUALDADE e sem piso de dígitos. O piso de 3 existe
            // para CPF e número externo, onde `Contains` com um ou dois dígitos devolveria meia
            // fila — mas a solicitação 7 é a 7, e exigir três dígitos a tornaria impossível de
            // achar até o município passar de cem pedidos. Achado pelo CI, num banco novo: a
            // bancada tem números altos de execuções anteriores e escondia isto.
            // O `&&` curto-circuita antes do TryParse, então o compilador não garante a
            // atribuição — daí a variável nascer explícita em vez de sair do `out`.
            long numeroLocal = 0;
            var ehNumeroLocal = digitos.Length == termo.Length && long.TryParse(termo, out numeroLocal);
            var buscaAmpla = digitos.Length >= 3;

            // Quatro formas de procurar a mesma solicitação, porque é assim que se procura no
            // balcão: pelo nome de quem está na frente, pelo documento, pelo nosso número, ou
            // pelo número que a pessoa traz num papel do sistema de lá.
            consulta = consulta.Where(s =>
                EF.Functions.ILike(s.PacienteNome, $"%{termo}%")
                || (ehNumeroLocal && s.NumeroLocal == numeroLocal)
                || (buscaAmpla && s.PacienteCpf != null && s.PacienteCpf.Contains(digitos))
                || (buscaAmpla && s.NumeroExterno != null && s.NumeroExterno.Contains(digitos)));
        }

        var total = await consulta.CountAsync(ct);

        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, 200);

        var itens = await consulta
            // Quem espera há mais tempo aparece primeiro na fila; dentro do mesmo instante, o
            // número local desempata para a ordenação ser estável entre páginas.
            .OrderBy(s => s.CriadoEm).ThenBy(s => s.NumeroLocal)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(s => new RegulacaoSolicitacaoListaDto(
                s.Id, s.NumeroLocal, s.NumeroExterno, s.SistemaDestino, s.Fluxo, s.Status,
                s.PacienteNome, s.PacienteCpf,
                s.Procedimento != null ? s.Procedimento.NomeCanonico : "(procedimento removido)",
                s.UnidadeSolicitanteId,
                s.UnidadeSolicitante != null ? s.UnidadeSolicitante.Nome : "(unidade removida)",
                s.UnidadeEmNomeDe != null ? s.UnidadeEmNomeDe.Nome : null,
                null,
                s.CriadoEm, s.AtualizadoEm))
            .ToListAsync(ct);

        // O nome do agente sai de uma segunda consulta, e não de um join por linha: são poucos
        // agentes e muitas solicitações.
        var agentes = await CarregarNomesDeAgentesAsync(consulta, pagina, tamanho, ct);
        var comAgente = itens
            .Select(i => agentes.TryGetValue(i.Id, out var nome) ? i with { AgenteNome = nome } : i)
            .ToList();

        return new PaginaSolicitacoesRegulacaoDto(total, comAgente);
    }

    public async Task<RegulacaoResumoFilaDto> ResumoAsync(CancellationToken ct)
    {
        var veTudo = await escopoRegulacao.EhAgenteAsync(ct);
        var consulta = await ConsultaNoEscopoAsync(ct);
        if (consulta is null)
        {
            return new RegulacaoResumoFilaDto(new Dictionary<StatusRegulacao, int>(), veTudo);
        }

        var contagens = await consulta
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        return new RegulacaoResumoFilaDto(
            contagens.ToDictionary(c => c.Status, c => c.Total), veTudo);
    }

    public async Task<RegulacaoSolicitacaoDetalheDto> CriarAsync(
        CriarRegulacaoSolicitacaoRequest req, CancellationToken ct)
    {
        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new ValidacaoException("usuario", "Sessão sem usuário — refaça o login.");

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        var unidadeSolicitante = escopo.Referencia
            ?? escopo.Unidades.FirstOrDefault();
        if (unidadeSolicitante == Guid.Empty)
        {
            throw new ValidacaoException(
                "unidade",
                "Escolha a unidade de origem no topo da tela — a solicitação pertence a uma unidade.");
        }

        // NAR é agendamento indireto: entra no SISREG em nome de OUTRA unidade, com a credencial
        // dela (D-9). Sem esse campo, o pedido não tem como ser incluído depois.
        if (req.Fluxo == FluxoRegulacao.Nar && req.UnidadeEmNomeDeId is null)
        {
            throw new ValidacaoException(
                "unidadeEmNomeDe", "No NAR é obrigatório informar a unidade em nome de quem a solicitação é aberta.");
        }
        if (req.Fluxo != FluxoRegulacao.Nar && req.UnidadeEmNomeDeId is not null)
        {
            throw new ValidacaoException(
                "unidadeEmNomeDe", "Só o fluxo NAR abre solicitação em nome de outra unidade.");
        }

        var procedimento = await db.RegulacaoProcedimentos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == req.ProcedimentoId && p.Ativo, ct)
            ?? throw new NaoEncontradoException("Procedimento canônico da regulação", req.ProcedimentoId);

        await ExigirDestinoPermitidoAsync(req.Fluxo, req.ProcedimentoId, ct);

        var (nome, cpf, cns) = await LerIdentidadePacienteAsync(req.PacienteId, ct);

        // NAR sempre termina no SISREG — não há escolha de destino.
        var destino = req.Fluxo == FluxoRegulacao.Nar ? SistemaRegulacao.Sisreg : req.SistemaDestino;

        var formulario = await formularios.ObterOuGerarAsync(req.ProcedimentoId, req.Fluxo, ct);

        var s = new RegulacaoSolicitacao
        {
            Id = Guid.CreateVersion7(),
            Fluxo = req.Fluxo,
            UnidadeSolicitanteId = unidadeSolicitante,
            UnidadeEmNomeDeId = req.UnidadeEmNomeDeId,
            CriadoPorUsuarioId = usuarioId,
            PacienteId = req.PacienteId,
            PacienteNome = nome,
            PacienteCpf = cpf,
            PacienteCns = cns,
            ProcedimentoId = req.ProcedimentoId,
            SistemaDestino = destino,
            FormularioVersaoId = formulario.VersaoId,
            FormularioJson = """{"canonico":{}}""",
            Status = StatusRegulacao.Rascunho,
            Observacoes = req.Observacoes,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioId,
        };
        db.RegulacaoSolicitacoes.Add(s);

        await eventos.RegistrarAsync(
            s.Id, TipoEventoRegulacao.Criacao, PapelEventoRegulacao.Solicitante, ct,
            para: StatusRegulacao.Rascunho,
            detalhe: new { fluxo = s.Fluxo.ToString(), procedimento = procedimento.NomeCanonico });

        // Solicitação e evento na mesma gravação: evento sem fato (ou fato sem evento) faria a
        // trilha mentir justamente onde ela é usada como prova.
        await db.SaveChangesAsync(ct);

        // A caixinha "Anexos gerais" existe desde o começo: os anexos precisam de dono antes de
        // o solicitante chegar ao passo do formulário.
        await exigencias.GarantirAnexosGeraisAsync(s.Id, ct);

        return await ObterAsync(s.Id, ct);
    }

    public async Task<RegulacaoSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct)
    {
        var s = await CarregarNoEscopoAsync(id, ct);
        var procedimento = await db.RegulacaoProcedimentos.AsNoTracking()
            .Where(p => p.Id == s.ProcedimentoId)
            .Select(p => p.NomeCanonico)
            .FirstOrDefaultAsync(ct) ?? "(procedimento removido)";

        return Mapear(s, procedimento);
    }

    public async Task<RegulacaoSolicitacaoDetalheDto> AtualizarAsync(
        Guid id, AtualizarRegulacaoSolicitacaoRequest req, CancellationToken ct)
    {
        var s = await CarregarNoEscopoAsync(id, ct, rastrear: true);

        if (s.Status is not (StatusRegulacao.Rascunho or StatusRegulacao.Devolvida))
        {
            throw new ConflitoException(
                "regulacao.solicitacao.nao_editavel",
                $"Uma solicitação em {s.Status} não é editável pela unidade solicitante.");
        }

        if (req.SistemaDestino is not null)
        {
            if (s.Fluxo == FluxoRegulacao.Nar && req.SistemaDestino != SistemaRegulacao.Sisreg)
            {
                throw new ValidacaoException("sistemaDestino", "O NAR sempre termina no SISREG.");
            }
            s.SistemaDestino = req.SistemaDestino;
        }

        IReadOnlyDictionary<string, object?>? diff = null;
        if (req.Formulario is { } f)
        {
            // O diff sai ANTES da sobrescrita — depois dela, o valor anterior já não existe.
            diff = eventos.Diferenca(LerCanonico(s.FormularioJson), f);

            // O formulário é guardado por sistema: `canonico` é o que a tela preenche, e a
            // tradução para `ser`/`sernit`/`sisreg` acontece no envio, com a versão gravada.
            s.FormularioJson = JsonSerializer.Serialize(new { canonico = f });
        }

        if (req.Observacoes is not null) s.Observacoes = req.Observacoes;

        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = usuarioAtual.UsuarioId;

        // Sem diff não há evento: "editou" sem dizer o quê polui a linha do tempo e esconde as
        // edições que importam. Salvar duas vezes a mesma coisa não vira duas linhas.
        if (diff is not null)
        {
            await eventos.RegistrarAsync(
                id, TipoEventoRegulacao.Edicao, PapelEventoRegulacao.Solicitante, ct, diff: diff);
        }

        await db.SaveChangesAsync(ct);

        return await ObterAsync(id, ct);
    }

    public async Task<IReadOnlyList<PendenciaEnvioDto>> PendenciasDeEnvioAsync(
        Guid id, CancellationToken ct)
    {
        var s = await CarregarNoEscopoAsync(id, ct);
        var config = await configuracao.ObterEntidadeAsync(ct);
        var pendencias = new List<PendenciaEnvioDto>();

        if (config.ExigirCpf && string.IsNullOrWhiteSpace(s.PacienteCpf))
        {
            // O SERNIT não grava sem CPF. Falhar aqui, com nome, é melhor do que falhar no envio.
            pendencias.Add(new PendenciaEnvioDto(
                "paciente.cpf",
                $"O paciente {s.PacienteNome} está sem CPF. Informe o CPF para enviar à regulação."));
        }

        if (s.SistemaDestino is null)
        {
            pendencias.Add(new PendenciaEnvioDto("destino", "Escolha o destino da solicitação."));
        }

        var formulario = await formularios.ObterOuGerarAsync(s.ProcedimentoId, s.Fluxo, ct);
        var canonico = LerCanonico(s.FormularioJson);
        foreach (var chave in formularios.ObrigatoriosFaltando(formulario, canonico))
        {
            var rotulo = formulario.Campos.FirstOrDefault(c => c.Chave == chave)?.Rotulo ?? chave;
            pendencias.Add(new PendenciaEnvioDto($"campo.{chave}", $"Preencha \"{rotulo}\"."));
        }

        var caixinhas = await exigencias.ListarAsync(id, ct);
        foreach (var e in caixinhas.Where(e => e.Obrigatoria
            && e.Situacao is SituacaoExigenciaRegulacao.Pendente or SituacaoExigenciaRegulacao.Criticada))
        {
            pendencias.Add(new PendenciaEnvioDto($"exigencia.{e.Id}", $"Anexe: {e.Titulo}."));
        }

        return pendencias;
    }

    public async Task<RegulacaoSolicitacaoDetalheDto> EnviarParaFilaAsync(Guid id, CancellationToken ct)
    {
        var s = await CarregarNoEscopoAsync(id, ct, rastrear: true);

        // Pergunta à máquina, e não a uma segunda lista de estados: duas fontes de verdade
        // divergem no dia em que alguém acrescenta uma transição em uma só delas. O motivo de
        // checar aqui, antes, é custo — calcular pendências consulta formulário, exigências e
        // configuração, e não faz sentido pagar isso para uma solicitação que já saiu do rascunho.
        if (!MaquinaDeEstadosRegulacao.PodeTransitar(
                s.Status, StatusRegulacao.PendenteRegulacao, PapelEventoRegulacao.Solicitante))
        {
            throw new ConflitoException(
                "regulacao.transicao_invalida",
                $"Uma solicitação em {s.Status} não vai para a fila.");
        }

        var pendencias = await PendenciasDeEnvioAsync(id, ct);
        if (pendencias.Count > 0)
        {
            throw new ValidacaoException(
                "pendencias",
                "Ainda falta: " + string.Join(" ", pendencias.Select(p => p.Descricao)));
        }

        // Aqui é onde, no incremento 7, o fluxo Interno inclui no SISREG com a credencial do
        // solicitante (D-8) antes de mudar de status. Enquanto o spike b não roda, a solicitação
        // interna entra na fila sem número e o agente registra o envio à mão (incremento 3).
        await TransitarAsync(s, StatusRegulacao.PendenteRegulacao, PapelEventoRegulacao.Solicitante, ct);
        s.StatusMotivo = null;
        await db.SaveChangesAsync(ct);

        return await ObterAsync(id, ct);
    }

    public async Task CancelarAsync(Guid id, string motivo, CancellationToken ct)
    {
        var s = await CarregarNoEscopoAsync(id, ct, rastrear: true);

        // Quem decide o que pode cancelar é a máquina de estados: depois de o agente assumir, a
        // ponta não puxa o tapete de quem já está trabalhando no caso.
        await TransitarAsync(
            s, StatusRegulacao.Cancelada, PapelEventoRegulacao.Solicitante, ct,
            detalhe: new { motivo });
        s.StatusMotivo = motivo;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RegulacaoEventoDto>> EventosAsync(Guid id, CancellationToken ct)
    {
        // Passa pelo escopo primeiro: a linha do tempo conta a história do paciente, e quem não
        // enxerga a solicitação não pode enxergá-la por esta porta.
        var s = await CarregarNoEscopoAsync(id, ct);
        return await eventos.ListarAsync(s.Id, ct);
    }

    // ---------------------------------------------------------------- apoio

    /// <summary>
    /// A consulta base já filtrada pelo escopo. <c>null</c> significa <b>sem acesso a unidade
    /// nenhuma</b> — e quem chama devolve conjunto vazio, em vez de deixar um `Contains` sobre
    /// array vazio decidir isso por acidente (ADR-0037, fail-closed).
    /// </summary>
    private async Task<IQueryable<RegulacaoSolicitacao>?> ConsultaNoEscopoAsync(CancellationToken ct)
    {
        var escopo = await escopoRegulacao.ResolverAsync(ct);
        if (escopo.SemAcesso) return null;

        var consulta = db.RegulacaoSolicitacoes.AsNoTracking()
            .Include(s => s.Procedimento)
            .Include(s => s.UnidadeSolicitante)
            .Include(s => s.UnidadeEmNomeDe)
            .Where(s => s.ExcluidoEm == null);

        if (escopo.VeTudo) return consulta;

        var unidades = escopo.Unidades;
        return consulta.Where(s => unidades.Contains(s.UnidadeSolicitanteId));
    }

    private async Task<Dictionary<Guid, string>> CarregarNomesDeAgentesAsync(
        IQueryable<RegulacaoSolicitacao> consulta, int pagina, int tamanho, CancellationToken ct)
    {
        var pares = await consulta
            .OrderBy(s => s.CriadoEm).ThenBy(s => s.NumeroLocal)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Where(s => s.AgenteResponsavelId != null)
            .Select(s => new { s.Id, AgenteId = s.AgenteResponsavelId!.Value })
            .ToListAsync(ct);
        if (pares.Count == 0) return [];

        var ids = pares.Select(p => p.AgenteId).Distinct().ToArray();
        var nomes = await db.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.NomeCompleto })
            .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);

        return pares
            .Where(p => nomes.ContainsKey(p.AgenteId))
            .ToDictionary(p => p.Id, p => nomes[p.AgenteId]);
    }

    /// <summary>
    /// Muda o estado passando pela máquina (plano 04): valida se a transição existe para aquele
    /// ator, grava o evento correspondente e carimba a alteração.
    ///
    /// <para><b>Não chama <c>SaveChanges</c></b> — quem chamou decide quando gravar, e assim o
    /// evento e a mudança vão juntos.</para>
    /// </summary>
    private async Task TransitarAsync(
        RegulacaoSolicitacao s,
        StatusRegulacao para,
        PapelEventoRegulacao papel,
        CancellationToken ct,
        object? diff = null,
        object? detalhe = null)
    {
        var de = s.Status;
        var evento = MaquinaDeEstadosRegulacao.EventoDe(de, para, papel);
        if (evento is null)
        {
            throw new ConflitoException(
                "regulacao.transicao_invalida",
                $"Uma solicitação em {de} não pode ir para {para} por esta ação.");
        }

        s.Status = para;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = usuarioAtual.UsuarioId;

        await eventos.RegistrarAsync(
            s.Id, evento.Value, papel, ct, de: de, para: para, diff: diff, detalhe: detalhe);
    }

    /// <summary>
    /// R-03: com oferta interna em Maricá, o Externo só passa se a configuração permitir.
    ///
    /// <para><b>A regra estava só na tela</b> (o wizard não oferecia o cartão "Externo"), e tela
    /// não é trava: um <c>POST</c> direto mandava o paciente para a fila do Estado com vaga
    /// existindo no município — exatamente o que a configuração existe para impedir. Aqui é o
    /// lugar dela; a tela continua escondendo a opção, o que é conveniência, não segurança.</para>
    ///
    /// <para>O NAR não passa por isto: ele é sempre SISREG, em nome de outra unidade.</para>
    /// </summary>
    private async Task ExigirDestinoPermitidoAsync(
        FluxoRegulacao fluxo, Guid procedimentoId, CancellationToken ct)
    {
        if (fluxo != FluxoRegulacao.Externo) return;

        var config = await configuracao.ObterEntidadeAsync(ct);
        if (config.PermitirExternoComInterno) return;

        var detalhe = await catalogo.ObterAsync(procedimentoId, ct);
        if (detalhe.ExecutantesInternos.Count == 0) return;

        var unidades = string.Join(", ", detalhe.ExecutantesInternos.Take(3).Select(u => u.Nome));
        throw new ValidacaoException(
            "fluxo",
            $"{detalhe.Nome} tem oferta em Maricá ({unidades}). A configuração do módulo não "
            + "permite mandar para fora havendo oferta interna — abra como Interno.");
    }

    /// <summary>
    /// Carrega respeitando o escopo por unidade. <b>Fail-closed</b>: fora do escopo devolve
    /// "não encontrado", e não "sem permissão" — dizer que existe já é vazar informação de que
    /// aquele paciente tem solicitação.
    /// </summary>
    private async Task<RegulacaoSolicitacao> CarregarNoEscopoAsync(
        Guid id, CancellationToken ct, bool rastrear = false)
    {
        var escopo = await escopoRegulacao.ResolverAsync(ct);

        var consulta = rastrear
            ? db.RegulacaoSolicitacoes.AsQueryable()
            : db.RegulacaoSolicitacoes.AsNoTracking();

        var s = await consulta.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", id);

        if (!escopo.VeTudo && !escopo.Unidades.Contains(s.UnidadeSolicitanteId))
        {
            throw new NaoEncontradoException("Solicitação da regulação", id);
        }

        return s;
    }

    private async Task<(string Nome, string? Cpf, string? Cns)> LerIdentidadePacienteAsync(
        Guid pacienteId, CancellationToken ct)
    {
        // O paciente vive no serviço FHIR, não neste DbContext — daí a leitura passar por
        // `IPacientesService`. A existência é conferida agora, e nome/CPF/CNS ficam copiados na
        // solicitação para a auditoria saber para quem o pedido foi feito NAQUELE dia: o
        // cadastro muda, a solicitação não.
        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        return (p.NomeCompleto, Nulo(p.Cpf), Nulo(p.Cns));
    }

    private static string? Nulo(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    private static JsonElement LerCanonico(string formularioJson)
    {
        try
        {
            var raiz = JsonDocument.Parse(formularioJson).RootElement;
            return raiz.TryGetProperty("canonico", out var c) ? c.Clone() : ObjetoVazio;
        }
        catch (JsonException)
        {
            return ObjetoVazio;
        }
    }

    private static RegulacaoSolicitacaoDetalheDto Mapear(RegulacaoSolicitacao s, string procedimentoNome) =>
        new(
            s.Id, s.NumeroLocal, s.Fluxo, s.Status, s.StatusMotivo,
            s.UnidadeSolicitanteId, s.UnidadeEmNomeDeId,
            s.PacienteId, s.PacienteNome, s.PacienteCpf,
            s.ProcedimentoId, procedimentoNome,
            s.SistemaDestino, s.FormularioVersaoId,
            LerCanonico(s.FormularioJson),
            s.NumeroExterno, s.Observacoes, s.CriadoEm);
}
