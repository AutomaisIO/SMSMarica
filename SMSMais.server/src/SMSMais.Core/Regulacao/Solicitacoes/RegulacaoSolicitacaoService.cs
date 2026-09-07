using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Catalogo;
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

public interface IRegulacaoSolicitacaoService
{
    Task<RegulacaoSolicitacaoDetalheDto> CriarAsync(
        CriarRegulacaoSolicitacaoRequest req, CancellationToken ct);

    Task<RegulacaoSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct);

    Task<RegulacaoSolicitacaoDetalheDto> AtualizarAsync(
        Guid id, AtualizarRegulacaoSolicitacaoRequest req, CancellationToken ct);

    /// <summary>O que falta para sair do rascunho. Lista vazia = pode enviar.</summary>
    Task<IReadOnlyList<PendenciaEnvioDto>> PendenciasDeEnvioAsync(Guid id, CancellationToken ct);

    Task<RegulacaoSolicitacaoDetalheDto> EnviarParaFilaAsync(Guid id, CancellationToken ct);

    Task CancelarAsync(Guid id, string motivo, CancellationToken ct);
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
    IPacientesService pacientes) : IRegulacaoSolicitacaoService
{
    private static readonly JsonElement ObjetoVazio = JsonDocument.Parse("{}").RootElement.Clone();

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

        if (req.Formulario is { } f)
        {
            // O formulário é guardado por sistema: `canonico` é o que a tela preenche, e a
            // tradução para `ser`/`sernit`/`sisreg` acontece no envio, com a versão gravada.
            s.FormularioJson = JsonSerializer.Serialize(new { canonico = f });
        }

        if (req.Observacoes is not null) s.Observacoes = req.Observacoes;

        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = usuarioAtual.UsuarioId;
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

        if (s.Status is not (StatusRegulacao.Rascunho or StatusRegulacao.Devolvida))
        {
            throw new ConflitoException(
                "regulacao.solicitacao.status_invalido",
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
        s.Status = StatusRegulacao.PendenteRegulacao;
        s.StatusMotivo = null;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return await ObterAsync(id, ct);
    }

    public async Task CancelarAsync(Guid id, string motivo, CancellationToken ct)
    {
        var s = await CarregarNoEscopoAsync(id, ct, rastrear: true);

        // Depois de o agente assumir, cancelar é decisão dele — a ponta não puxa o tapete de
        // quem já está trabalhando no caso.
        if (s.Status is not (StatusRegulacao.Rascunho or StatusRegulacao.PendenteRegulacao))
        {
            throw new ConflitoException(
                "regulacao.solicitacao.nao_cancelavel",
                $"Uma solicitação em {s.Status} não pode ser cancelada pela unidade solicitante.");
        }

        s.Status = StatusRegulacao.Cancelada;
        s.StatusMotivo = motivo;
        s.AtualizadoEm = DateTime.UtcNow;
        s.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- apoio

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
        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);

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
