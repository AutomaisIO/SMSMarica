using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Regras;

/// <summary>
/// O que a solicitação respondeu a uma regra, como ficou gravado — inclusive o que foi deduzido.
/// É a leitura do agente regulador: <b>não reavalia nada</b>, só mostra o veredito guardado.
/// </summary>
/// <param name="Opcoes">Pergunta de lista: todas as opções da versão respondida.</param>
/// <param name="OpcoesMarcadas">Pergunta de lista: o que o solicitante marcou.</param>
/// <param name="Vigente">
/// A regra respondida ainda vale. Falso = ela foi substituída depois (nova versão, desativada ou
/// trocada por uma pergunta de lista): a resposta fica como história, mas não decide mais nada.
/// </param>
public sealed record RespostaRegraRegistradaDto(
    Guid RegraId,
    int Versao,
    TipoRegraRegulacao Tipo,
    SeveridadeRegraRegulacao Severidade,
    SistemaRegulacao? Sistema,
    string Descricao,
    string? Pergunta,
    RespostaRegraRegulacao Resposta,
    ResultadoRegraRegulacao Resultado,
    string? Motivo,
    IReadOnlyList<string> Opcoes,
    IReadOnlyList<string> OpcoesMarcadas,
    DateTime RespondidoEm,
    bool Vigente);

public interface IRegulacaoElegibilidadeService
{
    /// <summary>Avalia e <b>persiste</b> o resultado: respostas deduzidas, destinos e caixinhas.</summary>
    Task<AvaliacaoElegibilidadeDto> AvaliarAsync(Guid solicitacaoId, CancellationToken ct);

    Task<AvaliacaoElegibilidadeDto> ResponderAsync(
        Guid solicitacaoId, IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas, CancellationToken ct);

    /// <param name="opcoesMarcadas">
    /// Pergunta de lista: as opções marcadas, por regra. Obrigatório quando a resposta a uma
    /// pergunta de lista é "Sim" — é isso que diz ao regulador qual condição vale.
    /// </param>
    Task<AvaliacaoElegibilidadeDto> ResponderAsync(
        Guid solicitacaoId,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? opcoesMarcadas,
        CancellationToken ct);

    /// <summary>O que já foi respondido e deduzido, sem reavaliar — a leitura do agente.</summary>
    Task<IReadOnlyList<RespostaRegraRegistradaDto>> RespostasAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>Exames do próprio SMSMais que servem para aquela caixinha.</summary>
    Task<IReadOnlyList<ExameParaRegras>> ExamesInternosAsync(
        Guid solicitacaoId, Guid exigenciaId, CancellationToken ct);

    /// <summary>
    /// Usa um exame que o SMSMais já tem como resposta à exigência (R-09): gera o PDF, anexa na
    /// caixinha e registra quem validou que aquele exame é o pedido.
    /// </summary>
    Task<ExigenciaDto> UsarExameInternoAsync(
        Guid solicitacaoId, Guid exigenciaId, Guid exameId, Guid? laudoId, CancellationToken ct);
}

/// <summary>
/// Junta o que o motor precisa — paciente, regras, respostas, exames — e guarda o veredito
/// (plano 03).
///
/// <para><b>O motor decide; este serviço busca e persiste.</b> A separação é o que permite testar
/// a régua clínica sem banco e, aqui, cuidar só de onde cada dado mora.</para>
///
/// <para><b>Avaliar é idempotente e reescreve o veredito.</b> Reavaliar depois de o solicitante
/// responder uma pergunta tem de mudar o destino — acumular avaliações antigas faria a tela
/// mostrar "bloqueado" e "liberado" ao mesmo tempo.</para>
/// </summary>
public sealed class RegulacaoElegibilidadeService(
    SmsMaisDbContext db,
    IRegulacaoEscopo escopoRegulacao,
    IRegulacaoConfiguracaoService configuracao,
    IRegulacaoExigenciaService exigencias,
    IPacientesService pacientes,
    ICidadaoClinicoService clinico,
    IUsuarioAtualAccessor usuarioAtual) : IRegulacaoElegibilidadeService
{
    public Task<AvaliacaoElegibilidadeDto> AvaliarAsync(Guid solicitacaoId, CancellationToken ct) =>
        AvaliarInternoAsync(solicitacaoId, null, null, ct);

    public Task<AvaliacaoElegibilidadeDto> ResponderAsync(
        Guid solicitacaoId,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        CancellationToken ct) =>
        AvaliarInternoAsync(solicitacaoId, respostas, null, ct);

    public Task<AvaliacaoElegibilidadeDto> ResponderAsync(
        Guid solicitacaoId,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? opcoesMarcadas,
        CancellationToken ct) =>
        AvaliarInternoAsync(solicitacaoId, respostas, opcoesMarcadas, ct);

    public async Task<IReadOnlyList<RespostaRegraRegistradaDto>> RespostasAsync(
        Guid solicitacaoId, CancellationToken ct)
    {
        await CarregarAsync(solicitacaoId, ct);

        // `IgnoreQueryFilters`: a resposta aponta para a versão que o solicitante viu, e essa
        // versão pode ter sido excluída depois — ela continua explicando o veredito.
        var linhas = await db.RegulacaoSolicitacaoRespostasRegra.AsNoTracking()
            .Where(r => r.SolicitacaoId == solicitacaoId)
            .Join(db.RegulacaoRegras.IgnoreQueryFilters().AsNoTracking(),
                r => r.RegraId, g => g.Id, (r, g) => new { Resposta = r, Regra = g })
            .ToListAsync(ct);

        return [.. linhas
            .OrderBy(x => x.Regra.Ordem).ThenBy(x => x.Regra.Descricao)
            .Select(x => new RespostaRegraRegistradaDto(
                x.Regra.Id, x.Resposta.RegraVersao, x.Regra.Tipo, x.Regra.Severidade, x.Regra.Sistema,
                x.Regra.Descricao, x.Regra.Pergunta, x.Resposta.Resposta, x.Resposta.Resultado,
                x.Resposta.ValorDeduzido, AvaliadorElegibilidade.OpcoesDa(x.Regra),
                ListaJson(x.Resposta.OpcoesMarcadasJson), x.Resposta.RespondidoEm,
                x.Regra.Ativo && x.Regra.ExcluidoEm == null))];
    }

    private async Task<AvaliacaoElegibilidadeDto> AvaliarInternoAsync(
        Guid solicitacaoId,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao>? novasRespostas,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>>? novasOpcoes,
        CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);
        var config = await configuracao.ObterEntidadeAsync(ct);

        var regras = await RegrasDoProcedimentoAsync(s.ProcedimentoId, ct);
        var paciente = await PacienteAsync(s.PacienteId, ct);
        var exames = await ExamesAsync(s.PacienteId, ct);
        var candidatos = await CandidatosAsync(s, ct);
        var cid = CidDaSolicitacao(s.FormularioJson);

        // As respostas já dadas continuam valendo; as novas entram por cima. Sem isso, responder
        // uma pergunta apagaria as anteriores e o solicitante recomeçaria o questionário.
        var gravadas = await db.RegulacaoSolicitacaoRespostasRegra.AsNoTracking()
            .Where(r => r.SolicitacaoId == solicitacaoId && r.Resposta != RespostaRegraRegulacao.Deduzido)
            .Select(r => new { r.RegraId, r.Resposta, r.OpcoesMarcadasJson })
            .ToListAsync(ct);
        var respostas = gravadas.ToDictionary(r => r.RegraId, r => r.Resposta);
        var opcoes = gravadas
            .Where(r => r.OpcoesMarcadasJson is not null)
            .ToDictionary(r => r.RegraId, r => ListaJson(r.OpcoesMarcadasJson));

        if (novasRespostas is not null)
        {
            var porId = regras.ToDictionary(r => r.Id);
            foreach (var (regraId, resposta) in novasRespostas)
            {
                respostas[regraId] = resposta;
                opcoes.Remove(regraId);

                if (porId.TryGetValue(regraId, out var regra)
                    && OpcoesValidas(regra, resposta, novasOpcoes?.GetValueOrDefault(regraId)) is { } marcadas)
                {
                    opcoes[regraId] = marcadas;
                }
            }
        }

        var avaliacao = AvaliadorElegibilidade.Avaliar(
            regras, paciente, cid, respostas, exames, candidatos,
            DateOnly.FromDateTime(DateTime.UtcNow), config.NaoSeiPadrao);

        await PersistirAsync(s, regras, avaliacao, respostas, opcoes, ct);
        return avaliacao;
    }

    /// <summary>
    /// As opções que valem para esta resposta. Só a pergunta de lista respondida com "Sim" guarda
    /// opções — e nela marcar ao menos uma é obrigatório: um "Sim" sem dizer qual condição é
    /// exatamente a informação que a lista existe para não perder.
    /// </summary>
    private static IReadOnlyList<string>? OpcoesValidas(
        RegulacaoRegra regra, RespostaRegraRegulacao resposta, IReadOnlyList<string>? marcadas)
    {
        var disponiveis = AvaliadorElegibilidade.OpcoesDa(regra);
        if (disponiveis.Count == 0 || resposta != RespostaRegraRegulacao.Sim) return null;

        var escolhidas = (marcadas ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim())
            .Distinct()
            .ToList();

        if (escolhidas.Count == 0)
        {
            throw new ValidacaoException(
                "opcoes", $"Marque ao menos uma opção em \"{regra.Pergunta ?? regra.Descricao}\".");
        }

        var estranha = escolhidas.FirstOrDefault(o => !disponiveis.Contains(o));
        if (estranha is not null)
        {
            // A regra mudou de versão entre abrir a tela e salvar, ou a opção veio digitada:
            // gravar texto que a regra não tem quebraria a leitura do regulador.
            throw new ValidacaoException(
                "opcoes", $"\"{estranha}\" não é uma opção desta pergunta. Recarregue a tela.");
        }

        // Na ordem da regra, não na ordem do clique: é como o regulador lê a lista.
        return [.. disponiveis.Where(escolhidas.Contains)];
    }

    private static IReadOnlyList<string> ListaJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<ExameParaRegras>> ExamesInternosAsync(
        Guid solicitacaoId, Guid exigenciaId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);

        var regraId = await db.RegulacaoSolicitacaoExigencias.AsNoTracking()
            .Where(e => e.Id == exigenciaId && e.SolicitacaoId == solicitacaoId)
            .Select(e => e.RegraId)
            .FirstOrDefaultAsync(ct);
        if (regraId is null) return [];

        var regra = await db.RegulacaoRegras.AsNoTracking().FirstOrDefaultAsync(r => r.Id == regraId, ct);
        if (regra?.TipoExameId is null) return [];

        var exames = await ExamesAsync(s.PacienteId, ct);
        var limite = regra.ValidadeDias is { } dias
            ? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-dias)
            : DateOnly.MinValue;

        return [.. exames
            .Where(e => e.TipoExameId == regra.TipoExameId && e.RealizadoEm >= limite)
            .OrderByDescending(e => e.Laudado).ThenByDescending(e => e.RealizadoEm)];
    }

    public async Task<ExigenciaDto> UsarExameInternoAsync(
        Guid solicitacaoId, Guid exigenciaId, Guid exameId, Guid? laudoId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);

        var exigencia = await db.RegulacaoSolicitacaoExigencias
            .FirstOrDefaultAsync(e => e.Id == exigenciaId && e.SolicitacaoId == solicitacaoId, ct)
            ?? throw new NaoEncontradoException("Exigência da solicitação", exigenciaId);

        // O exame tem de ser DAQUELE paciente. A checagem é do serviço clínico, que já resolve
        // conciliação por UID — refazer a conta aqui deixaria uma porta por onde o exame de
        // outra pessoa entraria na solicitação.
        var candidatos = await ExamesAsync(s.PacienteId, ct);
        var exame = candidatos.FirstOrDefault(e => e.Id == exameId)
            ?? throw new ValidacaoException(
                "exame", "Este exame não é do paciente da solicitação.");

        var idDoLaudo = laudoId ?? exame.LaudoId;

        // Prefere o laudo: é o que a regulação lê. Sem laudo, vai o PDF das imagens — serve de
        // comprovação de que o exame foi feito, que já é mais do que anexo nenhum.
        byte[]? conteudo = null;
        var nome = $"{exame.Descricao} - {exame.RealizadoEm:dd-MM-yyyy}.pdf";

        if (idDoLaudo is { } lid)
        {
            var pdf = await clinico.ObterLaudoPdfAsync(s.PacienteId, lid, ct);
            conteudo = pdf?.Conteudo;
            if (pdf is not null) nome = $"Laudo - {nome}";
        }

        conteudo ??= await clinico.ObterImagensPdfAsync(s.PacienteId, exameId, ct);

        if (conteudo is null || conteudo.Length == 0)
        {
            throw new ValidacaoException(
                "exame",
                "Não foi possível gerar o PDF deste exame. Anexe o arquivo manualmente.");
        }

        await exigencias.AnexarInternoAsync(
            solicitacaoId, exigenciaId, nome, "application/pdf", conteudo, ct);

        exigencia.Situacao = SituacaoExigenciaRegulacao.Atendida;
        exigencia.ExameInternoExameImagemId = exameId;
        exigencia.ExameInternoLaudoId = idDoLaudo;

        // Quem validou que aquele exame é o pedido é uma pessoa, e isso fica registrado: o
        // sistema ofereceu, alguém confirmou.
        exigencia.ValidadoExamePor = usuarioAtual.UsuarioId;
        exigencia.ValidadoExameEm = DateTime.UtcNow;
        exigencia.CriticaTexto = null;
        await db.SaveChangesAsync(ct);

        return (await exigencias.ListarAsync(solicitacaoId, ct)).First(e => e.Id == exigenciaId);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<RegulacaoSolicitacao> CarregarAsync(Guid id, CancellationToken ct)
    {
        var s = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", id);

        await escopoRegulacao.ExigirAlcanceAsync(s, ct);
        return s;
    }

    private async Task<IReadOnlyList<RegulacaoRegra>> RegrasDoProcedimentoAsync(
        Guid procedimentoId, CancellationToken ct) =>
        await db.RegulacaoRegras.AsNoTracking()
            .Where(r => r.ProcedimentoId == procedimentoId && r.Ativo)
            .OrderBy(r => r.Ordem)
            .ToListAsync(ct);

    private async Task<PacienteParaRegras> PacienteAsync(Guid pacienteId, CancellationToken ct)
    {
        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        return new PacienteParaRegras(
            p.DataNascimento,
            // O cadastro usa o enum FHIR; a regra do manual fala em "M"/"F". `Outro` e
            // `NaoInformado` viram null de propósito: uma regra de sexo sobre eles tem de ficar
            // indefinida e ser conferida por gente, não decidida por aproximação.
            p.Sexo switch
            {
                Sexo.Masculino => "M",
                Sexo.Feminino => "F",
                _ => null,
            },
            string.IsNullOrWhiteSpace(p.Cpf) ? null : p.Cpf,
            null);
    }

    /// <summary>
    /// Os exames do paciente, com o tipo.
    ///
    /// <para>Reusa <see cref="ICidadaoClinicoService.ListarExamesAsync"/> — que já sabe o que
    /// conta como exame do paciente, inclusive a conciliação por UID — e complementa com o
    /// <c>TipoExameId</c>, que aquele DTO não carrega, numa consulta só.</para>
    /// </summary>
    private async Task<IReadOnlyList<ExameParaRegras>> ExamesAsync(Guid pacienteId, CancellationToken ct)
    {
        var resumos = await clinico.ListarExamesAsync(pacienteId, ct);
        if (resumos.Count == 0) return [];

        var ids = resumos.Select(e => e.Id).ToArray();
        var tipos = await db.ExamesImagem.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.TipoExameId })
            .ToDictionaryAsync(e => e.Id, e => e.TipoExameId, ct);

        return [.. resumos.Select(e => new ExameParaRegras(
            e.Id,
            tipos.GetValueOrDefault(e.Id),
            DateOnly.FromDateTime(e.Data),
            e.LaudoId is not null,
            e.Nome,
            e.LaudoId))];
    }

    /// <summary>
    /// Para onde este pedido pode ir, antes das regras. Interno e NAR terminam no SISREG (D-9);
    /// o Externo depende de onde o procedimento existe.
    /// </summary>
    private async Task<IReadOnlyList<SistemaRegulacao>> CandidatosAsync(
        RegulacaoSolicitacao s, CancellationToken ct)
    {
        if (s.Fluxo is FluxoRegulacao.Interno or FluxoRegulacao.Nar) return [SistemaRegulacao.Sisreg];

        var comOrigem = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.ProcedimentoId == s.ProcedimentoId && o.Ativo && o.Sistema != SistemaRegulacao.Sisreg)
            .Select(o => o.Sistema)
            .Distinct()
            .ToListAsync(ct);

        return comOrigem;
    }

    /// <summary>O CID que a hipótese informou — o formulário canônico é quem o carrega.</summary>
    private static string? CidDaSolicitacao(string formularioJson)
    {
        try
        {
            var raiz = JsonDocument.Parse(formularioJson).RootElement;
            if (!raiz.TryGetProperty("canonico", out var c)) return null;

            foreach (var chave in new[] { "cid10", "cid", "cid_principal" })
            {
                if (c.TryGetProperty(chave, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var texto = v.GetString();
                    if (!string.IsNullOrWhiteSpace(texto)) return texto;
                }
            }
        }
        catch (JsonException)
        {
            // Formulário ilegível não pode derrubar a avaliação: segue sem CID.
        }
        return null;
    }

    private async Task PersistirAsync(
        RegulacaoSolicitacao s,
        IReadOnlyList<RegulacaoRegra> regras,
        AvaliacaoElegibilidadeDto avaliacao,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> opcoes,
        CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var usuarioId = usuarioAtual.UsuarioId;

        var existentes = await db.RegulacaoSolicitacaoRespostasRegra
            .Where(r => r.SolicitacaoId == s.Id)
            .ToDictionaryAsync(r => r.RegraId, ct);
        var porId = regras.ToDictionary(r => r.Id);

        foreach (var avaliada in avaliacao.Regras)
        {
            if (!porId.TryGetValue(avaliada.RegraId, out var regra)) continue;

            var resposta = respostas.GetValueOrDefault(avaliada.RegraId, RespostaRegraRegulacao.Deduzido);
            var marcadas = opcoes.TryGetValue(avaliada.RegraId, out var o) && o.Count > 0
                ? JsonSerializer.Serialize(o)
                : null;

            if (existentes.TryGetValue(avaliada.RegraId, out var linha))
            {
                linha.RegraVersao = regra.Versao;
                linha.Resposta = resposta;
                linha.Resultado = avaliada.Resultado;
                linha.ValorDeduzido = avaliada.Motivo;
                linha.OpcoesMarcadasJson = marcadas;
                linha.RespondidoEm = agora;
                linha.RespondidoPor = usuarioId;
                continue;
            }

            db.RegulacaoSolicitacaoRespostasRegra.Add(new RegulacaoSolicitacaoRespostaRegra
            {
                Id = Guid.CreateVersion7(),
                SolicitacaoId = s.Id,
                RegraId = avaliada.RegraId,
                RegraVersao = regra.Versao,
                Resposta = resposta,
                Resultado = avaliada.Resultado,
                ValorDeduzido = avaliada.Motivo,
                OpcoesMarcadasJson = marcadas,
                RespondidoEm = agora,
                RespondidoPor = usuarioId,
            });
        }

        // Os destinos são reescritos por inteiro: um veredito velho ao lado do novo faria a tela
        // mostrar "bloqueado" e "liberado" para o mesmo sistema.
        var destinos = await db.RegulacaoSolicitacaoDestinos
            .Where(d => d.SolicitacaoId == s.Id).ToListAsync(ct);
        db.RegulacaoSolicitacaoDestinos.RemoveRange(destinos);

        foreach (var sistema in avaliacao.DestinosPermitidos.Concat(avaliacao.MotivosDeBloqueio.Keys).Distinct())
        {
            var bloqueado = avaliacao.MotivosDeBloqueio.TryGetValue(sistema, out var motivo);
            db.RegulacaoSolicitacaoDestinos.Add(new RegulacaoSolicitacaoDestino
            {
                Id = Guid.CreateVersion7(),
                SolicitacaoId = s.Id,
                Sistema = sistema,
                Situacao = bloqueado
                    ? SituacaoDestinoRegulacao.Bloqueado
                    : avaliacao.DestinosComRessalva.Contains(sistema)
                        ? SituacaoDestinoRegulacao.ComRessalva
                        : SituacaoDestinoRegulacao.Elegivel,
                Motivo = motivo,
                AvaliadoEm = agora,
            });
        }

        await db.SaveChangesAsync(ct);

        // As caixinhas nascem depois da gravação: `GarantirDaRegraAsync` tem o próprio
        // `SaveChanges`, e uma exigência criada para uma regra que não persistiu ficaria órfã.
        foreach (var documento in avaliacao.DocumentosPendentes)
        {
            await exigencias.GarantirDaRegraAsync(
                s.Id, documento.RegraId, documento.Rotulo, documento.Obrigatorio, ct);
        }
    }
}
