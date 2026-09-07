using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Midias;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Regulacao.Legado;

/// <param name="UnidadeFallbackId">
/// Unidade a usar quando o autor do rascunho não tem vínculo nenhum em <c>usuario_unidade</c>.
/// Sem ela, esses rascunhos ficam de fora com o motivo registrado — <b>escolher uma unidade
/// qualquer os jogaria na fila da unidade errada</b>, e uma solicitação na fila errada é pior do
/// que uma solicitação que não migrou.
/// </param>
/// <param name="FecharTelasAntigas">
/// Marca <c>rascunhos_legados_migrados_em</c> ao terminar: as telas de rascunho SER/SERNIT viram
/// somente-leitura e as rotas de escrita passam a responder 410. Só faz sentido quando não sobrou
/// nada por migrar — a rota recusa fechar com pendência.
/// </param>
public sealed record MigrarRascunhosLegadosRequest(Guid? UnidadeFallbackId, bool FecharTelasAntigas);

/// <param name="Motivo">Por que este rascunho não virou solicitação. É o que a curadoria resolve.</param>
public sealed record RascunhoNaoMigradoDto(Guid RascunhoId, string Sistema, string Motivo);

public sealed record ResultadoMigracaoLegadoDto(
    int Ser,
    int Sernit,
    int JaMigrados,
    /// <summary>Rascunhos já enviados ou com falha — ficam onde estão, como histórico.</summary>
    int ForaDoEscopo,
    IReadOnlyList<RascunhoNaoMigradoDto> NaoMigrados,
    DateTime? TelasAntigasFechadasEm);

public interface IMigradorRascunhosLegadosService
{
    Task<ResultadoMigracaoLegadoDto> MigrarAsync(
        MigrarRascunhosLegadosRequest req, CancellationToken ct);

    /// <summary>Quantos rascunhos ainda esperam migração, sem escrever nada.</summary>
    Task<ResultadoMigracaoLegadoDto> PreviaAsync(CancellationToken ct);
}

/// <summary>
/// Leva os rascunhos por sistema (<c>ser_solicitacao_rascunho</c>, <c>sernit_*</c>) para
/// <c>regulacao_solicitacao</c> — tarefa 2.9 do plano 02.
///
/// <para><b>Nunca apaga o legado.</b> A tabela antiga fica intacta e a nova aponta para ela por
/// <c>origem_legado_id</c>; o <c>DropTable</c> é de uma release depois, quando a migração já tiver
/// sido conferida em produção. Copiar e apagar no mesmo passo tira o caminho de volta.</para>
///
/// <para><b>Idempotente</b> pela mesma coluna, com índice único parcial no banco: rodar duas
/// vezes não duplica, e dois cliques simultâneos não passam pela conferência juntos.</para>
///
/// <para><b>Rascunho que não tem para onde ir não é forçado.</b> Sem paciente no cadastro local,
/// sem procedimento canônico correspondente ou sem unidade do autor, ele fica onde está e entra
/// na lista de não migrados com o motivo. O modelo exige as três coisas (colunas obrigatórias), e
/// preencher qualquer uma delas por adivinhação produz uma solicitação que ninguém consegue
/// triar.</para>
/// </summary>
public sealed class MigradorRascunhosLegadosService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    IRegulacaoFormularioService formularios,
    IArquivoExigenciaStore store,
    IMidiasService midias,
    IRegulacaoConfiguracaoService configuracao,
    IRegulacaoEventoService eventos,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<MigradorRascunhosLegadosService> log) : IMigradorRascunhosLegadosService
{
    private const string TituloAnexosGerais = "Anexos gerais";

    public Task<ResultadoMigracaoLegadoDto> PreviaAsync(CancellationToken ct) =>
        ExecutarAsync(new MigrarRascunhosLegadosRequest(null, false), simular: true, ct);

    public Task<ResultadoMigracaoLegadoDto> MigrarAsync(
        MigrarRascunhosLegadosRequest req, CancellationToken ct) =>
        ExecutarAsync(req, simular: false, ct);

    private async Task<ResultadoMigracaoLegadoDto> ExecutarAsync(
        MigrarRascunhosLegadosRequest req, bool simular, CancellationToken ct)
    {
        var jaMigradas = await db.RegulacaoSolicitacoes.AsNoTracking()
            .Where(s => s.OrigemLegadoId != null)
            .Select(s => s.OrigemLegadoId!.Value)
            .ToHashSetAsync(ct);

        var naoMigrados = new List<RascunhoNaoMigradoDto>();
        var jaContados = 0;

        var rascunhosSer = await db.SerSolicitacaoRascunhos.AsNoTracking()
            .Include(r => r.Anexos)
            .ToListAsync(ct);
        var rascunhosSernit = await db.SernitSolicitacaoRascunhos.AsNoTracking()
            .Include(r => r.Anexos)
            .ToListAsync(ct);

        var foraDoEscopo =
            rascunhosSer.Count(r => r.Status is not (StatusRascunhoSer.Rascunho or StatusRascunhoSer.Pronto))
            + rascunhosSernit.Count(r => r.Status is not (StatusRascunhoSernit.Rascunho or StatusRascunhoSernit.Pronto));

        var migradosSer = 0;
        foreach (var r in rascunhosSer.Where(r =>
            r.Status is StatusRascunhoSer.Rascunho or StatusRascunhoSer.Pronto))
        {
            if (jaMigradas.Contains(r.Id)) { jaContados++; continue; }

            var chave = r.Tipo is { } tipo && !string.IsNullOrWhiteSpace(r.RecursoValor)
                ? $"{(int)tipo}|{r.RecursoValor}|{(r.AmbulatorioEstadual == true ? "AE" : "NAO_AE")}"
                : null;

            var resultado = await MigrarUmAsync(
                new RascunhoLegado(
                    r.Id, "SER", SistemaRegulacao.Ser, chave, r.Cns, r.PacienteNome, r.Hipotese,
                    r.CamposJson, r.CriadoPor, r.CriadoEm,
                    [.. r.Anexos.Select(a => new AnexoLegado(a.MidiaId, a.NomeArquivo, a.ContentType, a.CriadoEm))]),
                req, simular, ct);

            if (resultado is null) migradosSer++;
            else naoMigrados.Add(resultado);
        }

        var migradosSernit = 0;
        foreach (var r in rascunhosSernit.Where(r =>
            r.Status is StatusRascunhoSernit.Rascunho or StatusRascunhoSernit.Pronto))
        {
            if (jaMigradas.Contains(r.Id)) { jaContados++; continue; }

            var chave = r.Tipo is { } tipo && !string.IsNullOrWhiteSpace(r.RecursoValor)
                ? $"{(int)tipo}|{r.RecursoValor}"
                : null;

            var resultado = await MigrarUmAsync(
                new RascunhoLegado(
                    r.Id, "SERNIT", SistemaRegulacao.Sernit, chave, r.Cns, r.PacienteNome, r.Hipotese,
                    r.CamposJson, r.CriadoPor, r.CriadoEm,
                    [.. r.Anexos.Select(a => new AnexoLegado(a.MidiaId, a.NomeArquivo, a.ContentType, a.CriadoEm))]),
                req, simular, ct);

            if (resultado is null) migradosSernit++;
            else naoMigrados.Add(resultado);
        }

        DateTime? fechadasEm = (await configuracao.ObterEntidadeAsync(ct)).RascunhosLegadosMigradosEm;

        // Fechar com rascunho pendente deixaria alguém sem acesso ao próprio trabalho: a tela
        // antiga não aceita mais escrita e a solicitação nova nunca chegou a existir.
        if (!simular && req.FecharTelasAntigas && naoMigrados.Count == 0)
        {
            fechadasEm = DateTime.UtcNow;
            await configuracao.DefinirRascunhosLegadosMigradosAsync(fechadasEm, ct);
        }

        return new ResultadoMigracaoLegadoDto(
            migradosSer, migradosSernit, jaContados, foraDoEscopo, naoMigrados, fechadasEm);
    }

    /// <summary>Devolve <c>null</c> quando migrou; o motivo quando não deu.</summary>
    private async Task<RascunhoNaoMigradoDto?> MigrarUmAsync(
        RascunhoLegado r, MigrarRascunhosLegadosRequest req, bool simular, CancellationToken ct)
    {
        RascunhoNaoMigradoDto Recusa(string motivo) => new(r.Id, r.Sistema, motivo);

        if (r.ChaveExterna is null)
        {
            return Recusa("O rascunho não tem tipo/recurso escolhido — não há procedimento a que ligar.");
        }

        var procedimentoId = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Sistema == r.SistemaDestino && o.ChaveExterna == r.ChaveExterna)
            .Select(o => (Guid?)o.ProcedimentoId)
            .FirstOrDefaultAsync(ct);
        if (procedimentoId is null)
        {
            return Recusa(
                $"O recurso {r.ChaveExterna} não existe no catálogo canônico. "
                + "Rode a sincronização do catálogo da regulação e tente de novo.");
        }

        // A coluna chama-se `cns`, mas guarda o que o operador digitou no campo de documento do
        // SER — e ali cabe CPF **ou** CNS. Medido em produção (07/09/2026): dos 2 rascunhos, um
        // tem 15 dígitos (CNS) e o outro tem 11 (CPF). Exigir CNS recusaria metade deles com uma
        // mensagem que ainda por cima seria falsa ("está sem CNS" — ele tem documento).
        var documento = new string([.. (r.Cns ?? string.Empty).Where(char.IsDigit)]);
        if (documento.Length is not (11 or 15))
        {
            return Recusa(
                "O rascunho está sem CPF nem CNS do paciente — não dá para saber de quem é a solicitação.");
        }

        var ehCns = documento.Length == 15;
        var paciente = ehCns
            ? await pacientes.ObterPorCnsAsync(documento, ct)
            : await pacientes.ObterPorCpfAsync(documento, ct);
        if (paciente is null)
        {
            return Recusa(
                $"Nenhum paciente no cadastro com o {(ehCns ? "CNS" : "CPF")} {documento}. "
                + "Cadastre o paciente e rode a migração de novo.");
        }

        var unidadeId = await ResolverUnidadeAsync(r.CriadoPor, req.UnidadeFallbackId, ct);
        if (unidadeId is null)
        {
            return Recusa(
                "O autor do rascunho não tem unidade vinculada. Vincule-o a uma unidade, ou informe "
                + "a unidade de destino na migração.");
        }

        // A conferência do autor vem ANTES do corte da simulação: a prévia tem de recusar tudo o
        // que a migração recusaria, senão ela promete o que não cumpre.
        var autor = r.CriadoPor ?? usuarioAtual.UsuarioId;
        if (autor is null)
        {
            return Recusa("Rascunho sem autor e migração sem usuário na sessão — não há a quem atribuir.");
        }

        if (simular) return null;

        // O gerador recusa procedimento sem oferta externa viva (`ValidacaoException`). Não
        // capturar aqui faria UM rascunho com origem desativada derrubar a migração inteira —
        // e o operador veria "erro 400", sem saber qual dos rascunhos causou.
        RegulacaoFormularioDto formulario;
        try
        {
            formulario = await formularios.ObterOuGerarAsync(
                procedimentoId.Value, FluxoRegulacao.Externo, ct);
        }
        catch (Exception ex) when (ex is ValidacaoException or NaoEncontradoException)
        {
            return Recusa($"Não foi possível montar o formulário do procedimento: {ex.Message}");
        }

        var solicitacao = new RegulacaoSolicitacao
        {
            Id = Guid.CreateVersion7(),
            Fluxo = FluxoRegulacao.Externo,
            UnidadeSolicitanteId = unidadeId.Value,
            CriadoPorUsuarioId = autor.Value,
            PacienteId = paciente.Id,
            PacienteNome = string.IsNullOrWhiteSpace(r.PacienteNome) ? paciente.NomeCompleto : r.PacienteNome,
            PacienteCpf = string.IsNullOrWhiteSpace(paciente.Cpf) ? null : paciente.Cpf,
            // Só copia como CNS o que É um CNS. Gravar um CPF nesta coluna faria a solicitação
            // viajar com identificador errado para o sistema de destino.
            PacienteCns = ehCns ? documento : null,
            ProcedimentoId = procedimentoId.Value,
            SistemaDestino = r.SistemaDestino,
            FormularioVersaoId = formulario.VersaoId,

            // O preenchimento antigo entra no ramo do sistema de origem, e não em `canonico`: as
            // chaves são os nomes JSF do SER/SERNIT, não as chaves canônicas do formulário união.
            // Traduzir agora seria adivinhar; assim o agente vê o que a unidade tinha digitado.
            FormularioJson = MontarFormularioJson(r.SistemaDestino, r.CamposJson),

            Status = StatusRegulacao.Rascunho,
            Observacoes = MontarObservacao(r),
            OrigemLegadoId = r.Id,
            CriadoEm = r.CriadoEm,
            CriadoPor = autor,
            AtualizadoEm = DateTime.UtcNow,
            AtualizadoPor = usuarioAtual.UsuarioId,
        };
        db.RegulacaoSolicitacoes.Add(solicitacao);

        var exigencia = new RegulacaoSolicitacaoExigencia
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacao.Id,
            RegraId = null,
            Titulo = TituloAnexosGerais,
            Obrigatoria = false,
            Situacao = SituacaoExigenciaRegulacao.Pendente,
            Ordem = 1000,
        };
        db.RegulacaoSolicitacaoExigencias.Add(exigencia);

        var versao = 0;
        foreach (var anexo in r.Anexos.OrderBy(a => a.CriadoEm))
        {
            var conteudo = await midias.ObterConteudoAsync(anexo.MidiaId, ct);
            if (conteudo is null)
            {
                // Mídia sumida não impede a solicitação de existir — o resto do rascunho vale
                // mais do que o anexo perdido. Fica no log para quem for conferir.
                log.LogWarning(
                    "Anexo {Midia} do rascunho {Rascunho} não foi encontrado em midia — solicitação migrada sem ele.",
                    anexo.MidiaId, r.Id);
                continue;
            }

            var arquivoId = Guid.CreateVersion7();
            var tipo = string.IsNullOrWhiteSpace(anexo.ContentType)
                ? "application/octet-stream"
                : anexo.ContentType.Split(';')[0].Trim().ToLowerInvariant();
            var chaveArmazenamento = store.MontarChave(paciente.Id, arquivoId, ExtensaoDe(anexo.Nome, tipo));

            // Spaces antes da linha, como no serviço de exigências: registro apontando para
            // conteúdo inexistente é pior do que anexo que não migrou.
            await store.SalvarAsync(chaveArmazenamento, conteudo.Conteudo, ct);

            db.RegulacaoExigenciaArquivos.Add(new RegulacaoExigenciaArquivo
            {
                Id = arquivoId,
                ExigenciaId = exigencia.Id,
                ChaveArmazenamento = chaveArmazenamento,
                Nome = anexo.Nome,
                ContentType = tipo,
                Tamanho = conteudo.Conteudo.LongLength,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo.Conteudo)),
                Versao = ++versao,
                Situacao = SituacaoArquivoExigencia.Atual,
                Origem = OrigemArquivoExigencia.Upload,
                CriadoEm = anexo.CriadoEm,
                CriadoPor = autor,
            });
            exigencia.Situacao = SituacaoExigenciaRegulacao.Atendida;
        }

        // A trilha nasceu na tarefa 3.1, depois deste migrador, e ninguém as ligou: a primeira
        // solicitação migrada em produção (07/09/2026) ficou com a linha do tempo VAZIA. Quem
        // abrisse não veria de onde ela veio — e "de onde veio" é justamente o que explica uma
        // solicitação que aparece na fila sem ninguém ter aberto pela tela.
        await eventos.RegistrarAsync(
            solicitacao.Id, TipoEventoRegulacao.Criacao, PapelEventoRegulacao.Sistema, ct,
            para: StatusRegulacao.Rascunho,
            detalhe: new
            {
                migradoDe = r.Sistema,
                rascunhoLegadoId = r.Id,
                rascunhoCriadoEm = r.CriadoEm,
                anexosCopiados = versao,
            });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Uma migração concorrente ganhou a corrida (índice único em origem_legado_id), ou
            // uma FK não fechou. Nos dois casos: este rascunho fica de fora, os outros seguem.
            log.LogWarning(ex, "Falha ao migrar o rascunho {Rascunho} do {Sistema}.", r.Id, r.Sistema);

            // Desanexa SÓ o que este rascunho acrescentou. `ChangeTracker.Clear()` seria mais
            // curto e derrubaria também o que outro serviço do mesmo scope ainda vai gravar —
            // o `SaveChanges` seguinte simplesmente não escreveria nada, sem erro nenhum.
            db.Entry(solicitacao).State = EntityState.Detached;
            db.Entry(exigencia).State = EntityState.Detached;
            foreach (var entrada in db.ChangeTracker.Entries<RegulacaoExigenciaArquivo>()
                .Where(e => e.Entity.ExigenciaId == exigencia.Id).ToList())
            {
                entrada.State = EntityState.Detached;
            }
            foreach (var entrada in db.ChangeTracker.Entries<RegulacaoEvento>()
                .Where(e => e.Entity.SolicitacaoId == solicitacao.Id).ToList())
            {
                entrada.State = EntityState.Detached;
            }

            return Recusa($"Falha ao gravar a solicitação: {ex.InnerException?.Message ?? ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Unidade principal do autor; senão a primeira vinculada; senão a informada na migração.
    /// <c>null</c> quando não há nenhuma — e aí o rascunho não migra.
    /// </summary>
    private async Task<Guid?> ResolverUnidadeAsync(
        Guid? autorId, Guid? fallbackId, CancellationToken ct)
    {
        if (autorId is { } autor)
        {
            var vinculos = await db.UsuarioUnidades.AsNoTracking()
                .Where(v => v.UsuarioId == autor && v.Unidade!.Ativo)
                .OrderByDescending(v => v.Principal)
                .ThenBy(v => v.CriadoEm)
                .Select(v => v.UnidadeId)
                .FirstOrDefaultAsync(ct);
            if (vinculos != Guid.Empty) return vinculos;
        }

        if (fallbackId is { } fallback
            && await db.Unidades.AsNoTracking().AnyAsync(u => u.Id == fallback && u.Ativo, ct))
        {
            return fallback;
        }

        return null;
    }

    private static string MontarFormularioJson(SistemaRegulacao sistema, string camposJson)
    {
        var ramo = sistema == SistemaRegulacao.Ser ? "ser" : "sernit";
        JsonElement campos;
        try
        {
            campos = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(camposJson) ? "{}" : camposJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            campos = JsonDocument.Parse("{}").RootElement.Clone();
        }

        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteStartObject("canonico");
            w.WriteEndObject();
            w.WritePropertyName(ramo);
            campos.WriteTo(w);
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// A hipótese vem de campo livre do rascunho e `observacoes` tem teto de 4000: sem o corte,
    /// um texto colado de laudo derrubaria a migração daquele rascunho por comprimento — e o
    /// motivo apareceria como "falha ao gravar", que não ajuda ninguém.
    /// </summary>
    private static string MontarObservacao(RascunhoLegado r)
    {
        const int Teto = 4000;
        var texto = $"Migrada do rascunho do {r.Sistema} de {r.CriadoEm:dd/MM/yyyy}.";
        if (!string.IsNullOrWhiteSpace(r.Hipotese))
        {
            texto = $"{texto} Hipótese informada: {r.Hipotese.Trim()}";
        }
        return texto.Length <= Teto ? texto : texto[..(Teto - 1)] + "…";
    }

    private static string ExtensaoDe(string nome, string contentType)
    {
        var ext = Path.GetExtension(nome).Trim('.', ' ').ToLowerInvariant();
        if (!string.IsNullOrEmpty(ext)) return ext;

        return contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "application/pdf" => "pdf",
            _ => "bin",
        };
    }

    /// <summary>Forma comum entre o rascunho do SER e o do SERNIT — o que a migração precisa.</summary>
    private sealed record RascunhoLegado(
        Guid Id,
        string Sistema,
        SistemaRegulacao SistemaDestino,
        string? ChaveExterna,
        string? Cns,
        string? PacienteNome,
        string? Hipotese,
        string CamposJson,
        Guid? CriadoPor,
        DateTime CriadoEm,
        IReadOnlyList<AnexoLegado> Anexos);

    private sealed record AnexoLegado(Guid MidiaId, string Nome, string? ContentType, DateTime CriadoEm);
}
