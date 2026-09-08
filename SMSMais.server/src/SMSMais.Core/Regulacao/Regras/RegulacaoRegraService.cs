using System.Globalization;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Regras;

public sealed record SalvarRegulacaoRegraRequest(
    Guid ProcedimentoId,
    Guid? ProcedimentoOrigemId,
    SistemaRegulacao? Sistema,
    TipoRegraRegulacao Tipo,
    SeveridadeRegraRegulacao Severidade,
    string Descricao,
    string? Fonte,
    int? IdadeMinAnos,
    int? IdadeMaxAnos,
    string? Sexo,
    bool ExigeCpf,
    string[]? CidsPermitidos,
    string[]? CidsExcluidos,
    string? Pergunta,
    RespostaRegraRegulacao? RespostaBloqueia,
    NaoSeiViraRegulacao? NaoSeiVira,
    string? DocumentoRotulo,
    Guid? TipoExameId,
    int? ValidadeDias,
    bool Obrigatorio,
    int Ordem);

public sealed record RegulacaoRegraDto(
    Guid Id,
    Guid ProcedimentoId,
    Guid? ProcedimentoOrigemId,
    SistemaRegulacao? Sistema,
    TipoRegraRegulacao Tipo,
    SeveridadeRegraRegulacao Severidade,
    string Descricao,
    string? Fonte,
    int? IdadeMinAnos,
    int? IdadeMaxAnos,
    string? Sexo,
    bool ExigeCpf,
    string[] CidsPermitidos,
    string[] CidsExcluidos,
    string? Pergunta,
    RespostaRegraRegulacao? RespostaBloqueia,
    NaoSeiViraRegulacao? NaoSeiVira,
    string? DocumentoRotulo,
    Guid? TipoExameId,
    int? ValidadeDias,
    bool Obrigatorio,
    int Ordem,
    int Versao,
    bool Ativo,
    DateTime CriadoEm);

/// <param name="SemRecurso">Linhas cujo recurso não casou com o catálogo — vão para curadoria.</param>
public sealed record ImportacaoRegrasResultadoDto(
    int Lidas, int Criadas, int SemRecurso, IReadOnlyList<string> Avisos);

public interface IRegulacaoRegraService
{
    Task<IReadOnlyList<RegulacaoRegraDto>> ListarAsync(
        Guid procedimentoId, SistemaRegulacao? sistema, bool incluirInativas, CancellationToken ct);

    Task<RegulacaoRegraDto> CriarAsync(SalvarRegulacaoRegraRequest req, CancellationToken ct);

    /// <summary>Cria a versão seguinte e desativa a anterior — regra não se edita no lugar.</summary>
    Task<RegulacaoRegraDto> NovaVersaoAsync(Guid id, SalvarRegulacaoRegraRequest req, CancellationToken ct);

    Task AtivarAsync(Guid id, bool ativo, CancellationToken ct);

    Task ExcluirAsync(Guid id, CancellationToken ct);

    /// <summary>Importa o CSV extraído dos manuais. Cria tudo <b>inativo</b>.</summary>
    Task<ImportacaoRegrasResultadoDto> ImportarCsvAsync(Stream csv, CancellationToken ct);
}

/// <summary>
/// Cadastro das regras de elegibilidade (plano 03).
///
/// <para><b>Regra não se edita no lugar.</b> Corrigir cria a versão seguinte e desativa a
/// anterior — a resposta que o solicitante deu continua apontando para a versão que ele viu. Sem
/// isso, atualizar o manual reescreveria retroativamente por que um pedido foi barrado.</para>
/// </summary>
public sealed class RegulacaoRegraService(
    SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IRegulacaoRegraService
{
    public async Task<IReadOnlyList<RegulacaoRegraDto>> ListarAsync(
        Guid procedimentoId, SistemaRegulacao? sistema, bool incluirInativas, CancellationToken ct)
    {
        var consulta = db.RegulacaoRegras.AsNoTracking()
            .Where(r => r.ProcedimentoId == procedimentoId);

        if (sistema is { } s) consulta = consulta.Where(r => r.Sistema == s || r.Sistema == null);
        if (!incluirInativas) consulta = consulta.Where(r => r.Ativo);

        var regras = await consulta
            .OrderBy(r => r.Ordem).ThenBy(r => r.Descricao)
            .ToListAsync(ct);

        return [.. regras.Select(Mapear)];
    }

    public async Task<RegulacaoRegraDto> CriarAsync(
        SalvarRegulacaoRegraRequest req, CancellationToken ct)
    {
        Validar(req);

        var regra = Preencher(new RegulacaoRegra
        {
            Id = Guid.CreateVersion7(),
            Versao = 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        }, req);

        db.RegulacaoRegras.Add(regra);
        await db.SaveChangesAsync(ct);
        return Mapear(regra);
    }

    public async Task<RegulacaoRegraDto> NovaVersaoAsync(
        Guid id, SalvarRegulacaoRegraRequest req, CancellationToken ct)
    {
        Validar(req);

        var anterior = await db.RegulacaoRegras.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NaoEncontradoException("Regra de elegibilidade", id);

        anterior.Ativo = false;
        anterior.AtualizadoEm = DateTime.UtcNow;
        anterior.AtualizadoPor = usuarioAtual.UsuarioId;

        var nova = Preencher(new RegulacaoRegra
        {
            Id = Guid.CreateVersion7(),
            Versao = anterior.Versao + 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        }, req);

        db.RegulacaoRegras.Add(nova);
        await db.SaveChangesAsync(ct);
        return Mapear(nova);
    }

    public async Task AtivarAsync(Guid id, bool ativo, CancellationToken ct)
    {
        var regra = await db.RegulacaoRegras.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NaoEncontradoException("Regra de elegibilidade", id);

        regra.Ativo = ativo;
        regra.AtualizadoEm = DateTime.UtcNow;
        regra.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct)
    {
        var regra = await db.RegulacaoRegras.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NaoEncontradoException("Regra de elegibilidade", id);

        regra.ExcluidoEm = DateTime.UtcNow;
        regra.ExcluidoPor = usuarioAtual.UsuarioId;
        regra.Ativo = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Importa o CSV do spike e (1.169 regras dos manuais CRECE e REUNI).
    ///
    /// <para><b>Tudo entra inativo, e isso não é excesso de zelo.</b> O extrator classificou 974
    /// linhas como pergunta; ativar todas transformaria o wizard num questionário de vinte itens
    /// por procedimento, e o solicitante pararia de ler na terceira. A curadoria decide o que
    /// vira pergunta de verdade, o que vira texto informativo e o que não entra.</para>
    ///
    /// <para><b>A seção do manual decide qual resposta bloqueia</b>, e não o texto: critério de
    /// <c>inclusao</c> barra quem responde "não" (não preenche o critério); critério de
    /// <c>exclusao</c> barra quem responde "sim". Deduzir isso da frase invertia 295 das 1.169
    /// regras — foi medido no spike.</para>
    /// </summary>
    public async Task<ImportacaoRegrasResultadoDto> ImportarCsvAsync(Stream csv, CancellationToken ct)
    {
        using var leitor = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var registros = SepararCsv(await leitor.ReadToEndAsync(ct));
        if (registros.Count == 0)
        {
            throw new ValidacaoException("csv", "O arquivo está vazio.");
        }

        var colunas = registros[0]
            .Select((nome, i) => (nome: nome.Trim().TrimStart('﻿').ToLowerInvariant(), i))
            .ToDictionary(x => x.nome, x => x.i);

        foreach (var obrigatoria in new[] { "sistema", "tipo", "texto_original", "secao" })
        {
            if (!colunas.ContainsKey(obrigatoria))
            {
                throw new ValidacaoException("csv", $"Falta a coluna \"{obrigatoria}\" no cabeçalho.");
            }
        }

        // O catálogo inteiro do SER/SERNIT numa consulta: casar linha a linha faria 1.169 idas
        // ao banco.
        var origens = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Sistema != SistemaRegulacao.Sisreg)
            .Select(o => new { o.Id, o.ProcedimentoId, o.Sistema, o.RotuloExterno, o.Ramo })
            .ToListAsync(ct);

        var indice = new Dictionary<string, (Guid OrigemId, Guid ProcedimentoId)>(StringComparer.Ordinal);
        foreach (var o in origens)
        {
            indice.TryAdd(Chave(o.Sistema, o.RotuloExterno, o.Ramo), (o.Id, o.ProcedimentoId));
        }

        var avisos = new List<string>();
        var lidas = 0;
        var criadas = 0;
        var semRecurso = 0;
        var agora = DateTime.UtcNow;
        var usuarioId = usuarioAtual.UsuarioId;

        for (var indiceRegistro = 1; indiceRegistro < registros.Count; indiceRegistro++)
        {
            var numero = indiceRegistro + 1;
            var c = registros[indiceRegistro];
            if (c.All(string.IsNullOrWhiteSpace)) continue;

            lidas++;

            string Campo(string nome) =>
                colunas.TryGetValue(nome, out var i) && i < c.Length ? c[i].Trim() : string.Empty;

            var texto = Campo("texto_original");
            if (texto.Length == 0) continue;

            if (!Enum.TryParse<SistemaRegulacao>(Campo("sistema"), true, out var sistema))
            {
                avisos.Add($"linha {numero}: sistema \"{Campo("sistema")}\" desconhecido.");
                continue;
            }

            if (!Enum.TryParse<TipoRegraRegulacao>(Campo("tipo"), true, out var tipo))
            {
                avisos.Add($"linha {numero}: tipo \"{Campo("tipo")}\" desconhecido.");
                continue;
            }

            var rotulo = Campo("recurso_catalogo");
            if (rotulo.Length == 0) rotulo = Campo("recurso");

            var ramo = sistema == SistemaRegulacao.Ser
                ? Campo("ramo_ser") == "AmbulatorioEstadual" ? "AE" : "NAO_AE"
                : null;

            if (!indice.TryGetValue(Chave(sistema, rotulo, ramo), out var alvo))
            {
                semRecurso++;
                if (avisos.Count < 30)
                {
                    avisos.Add($"linha {numero}: recurso \"{rotulo}\" não existe no catálogo ({sistema}).");
                }
                continue;
            }

            // A seção manda: inclusão barra o "não", exclusão barra o "sim".
            var respostaBloqueia = tipo == TipoRegraRegulacao.NaoDedutivel
                ? Campo("secao").Equals("exclusao", StringComparison.OrdinalIgnoreCase)
                    ? RespostaRegraRegulacao.Sim
                    : RespostaRegraRegulacao.Nao
                : (RespostaRegraRegulacao?)null;

            db.RegulacaoRegras.Add(new RegulacaoRegra
            {
                Id = Guid.CreateVersion7(),
                ProcedimentoId = alvo.ProcedimentoId,
                ProcedimentoOrigemId = alvo.OrigemId,
                Sistema = sistema,
                Tipo = tipo,
                Severidade = SeveridadeRegraRegulacao.Bloqueia,
                Descricao = Cortar(texto, 2000)!,
                Fonte = Cortar(Campo("fonte"), 300),
                IdadeMinAnos = Inteiro(Campo("idade_min")),
                IdadeMaxAnos = Inteiro(Campo("idade_max")),
                Sexo = Campo("sexo") is "M" or "F" ? Campo("sexo") : null,
                Pergunta = tipo == TipoRegraRegulacao.NaoDedutivel
                    ? Cortar(Campo("pergunta") is { Length: > 0 } p ? p : texto, 500)
                    : null,
                RespostaBloqueia = respostaBloqueia,
                DocumentoRotulo = tipo == TipoRegraRegulacao.Documental
                    ? Cortar(Campo("documento") is { Length: > 0 } d ? d : texto, 200)
                    : null,
                Obrigatorio = true,
                Ordem = 0,
                Versao = 1,

                // Inativa: quem decide o que vira pergunta de verdade é a curadoria.
                Ativo = false,
                CriadoEm = agora,
                CriadoPor = usuarioId,
            });
            criadas++;

            // Lotes: 1.169 inserções num `SaveChanges` só é uma transação longa demais.
            if (criadas % 200 == 0) await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);

        if (semRecurso > 0)
        {
            avisos.Add(
                $"{semRecurso} linha(s) sem recurso no catálogo. Rode a sincronização do catálogo "
                + "e importe de novo, ou cadastre essas regras à mão.");
        }
        avisos.Add(
            $"As {criadas} regras entraram INATIVAS. Reveja-as antes de ativar: o extrator "
            + "classificou como pergunta muito do que é texto clínico do manual.");

        return new ImportacaoRegrasResultadoDto(lidas, criadas, semRecurso, avisos);
    }

    // ---------------------------------------------------------------- apoio

    /// <summary>
    /// Separa o CSV em registros e campos <b>honrando as aspas</b> (RFC 4180): campo entre aspas
    /// pode conter <c>;</c>, quebra de linha e aspas duplicadas (<c>""</c>).
    ///
    /// <para>Não é preciosismo: <b>98 das 1.169 linhas do manual vêm entre aspas</b>, porque o
    /// texto clínico usa ponto e vírgula ("Pneumonia de repetição (mais de 2 no último ano);
    /// infecções de repetição…"). Um <c>Split(';')</c> corta o critério ao meio e empurra o resto
    /// das colunas uma casa para a direita — o pedaço final do texto vira a "fonte" da regra, que
    /// é justamente a linha que a unidade solicitante lê para saber de onde veio a exigência.</para>
    /// </summary>
    private static List<string[]> SepararCsv(string texto)
    {
        var registros = new List<string[]>();
        var campos = new List<string>();
        var campo = new StringBuilder();
        var dentroDeAspas = false;
        var temConteudo = false;

        void FecharCampo()
        {
            campos.Add(campo.ToString());
            campo.Clear();
        }

        void FecharRegistro()
        {
            FecharCampo();
            if (temConteudo) registros.Add([.. campos]);
            campos.Clear();
            temConteudo = false;
        }

        for (var i = 0; i < texto.Length; i++)
        {
            var ch = texto[i];

            if (dentroDeAspas)
            {
                if (ch != '"')
                {
                    campo.Append(ch);
                    continue;
                }

                // "" dentro de campo entre aspas é uma aspa literal.
                if (i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    campo.Append('"');
                    i++;
                    continue;
                }

                dentroDeAspas = false;
                continue;
            }

            switch (ch)
            {
                case '"' when campo.Length == 0:
                    dentroDeAspas = true;
                    temConteudo = true;
                    break;
                case ';':
                    FecharCampo();
                    temConteudo = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    FecharRegistro();
                    break;
                default:
                    campo.Append(ch);
                    if (!char.IsWhiteSpace(ch)) temConteudo = true;
                    break;
            }
        }

        if (campo.Length > 0 || campos.Count > 0) FecharRegistro();
        return registros;
    }

    /// <summary>Rótulo sem acento, sem pontuação e em caixa alta — os catálogos variam a grafia.</summary>
    private static string Chave(SistemaRegulacao sistema, string rotulo, string? ramo) =>
        $"{sistema}|{ChaveRotulo.Normalizar(rotulo)}|{ramo}";

    private static void Validar(SalvarRegulacaoRegraRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Descricao))
        {
            throw new ValidacaoException("descricao", "A regra precisa do texto do manual.");
        }

        switch (req.Tipo)
        {
            case TipoRegraRegulacao.Dedutivel
                when req.IdadeMinAnos is null && req.IdadeMaxAnos is null
                    && string.IsNullOrWhiteSpace(req.Sexo) && !req.ExigeCpf
                    && (req.CidsPermitidos?.Length ?? 0) == 0 && (req.CidsExcluidos?.Length ?? 0) == 0:
                throw new ValidacaoException(
                    "tipo", "Regra dedutível precisa de ao menos um critério (idade, sexo, CPF ou CID).");

            case TipoRegraRegulacao.NaoDedutivel when string.IsNullOrWhiteSpace(req.Pergunta):
                throw new ValidacaoException("pergunta", "Regra não dedutível precisa da pergunta.");

            case TipoRegraRegulacao.NaoDedutivel when req.RespostaBloqueia is null:
                throw new ValidacaoException(
                    "respostaBloqueia",
                    "Diga qual resposta barra — ela vem da seção do manual, não do texto.");

            case TipoRegraRegulacao.Documental when string.IsNullOrWhiteSpace(req.DocumentoRotulo):
                throw new ValidacaoException("documentoRotulo", "Regra documental precisa do nome do documento.");
        }

        if (req.IdadeMinAnos is { } min && req.IdadeMaxAnos is { } max && min > max)
        {
            throw new ValidacaoException("idade", "A idade mínima não pode ser maior que a máxima.");
        }

        if (!string.IsNullOrWhiteSpace(req.Sexo) && req.Sexo is not ("M" or "F"))
        {
            throw new ValidacaoException("sexo", "Sexo da regra só pode ser M, F ou vazio.");
        }
    }

    private static RegulacaoRegra Preencher(RegulacaoRegra r, SalvarRegulacaoRegraRequest req)
    {
        r.ProcedimentoId = req.ProcedimentoId;
        r.ProcedimentoOrigemId = req.ProcedimentoOrigemId;
        r.Sistema = req.Sistema;
        r.Tipo = req.Tipo;
        r.Severidade = req.Severidade;
        r.Descricao = req.Descricao.Trim();
        r.Fonte = req.Fonte?.Trim();
        r.IdadeMinAnos = req.IdadeMinAnos;
        r.IdadeMaxAnos = req.IdadeMaxAnos;
        r.Sexo = string.IsNullOrWhiteSpace(req.Sexo) ? null : req.Sexo.ToUpperInvariant();
        r.ExigeCpf = req.ExigeCpf;
        r.CidsPermitidosJson = Json(req.CidsPermitidos);
        r.CidsExcluidosJson = Json(req.CidsExcluidos);
        r.Pergunta = req.Pergunta?.Trim();
        r.RespostaBloqueia = req.RespostaBloqueia;
        r.NaoSeiVira = req.NaoSeiVira;
        r.DocumentoRotulo = req.DocumentoRotulo?.Trim();
        r.TipoExameId = req.TipoExameId;
        r.ValidadeDias = req.ValidadeDias;
        r.Obrigatorio = req.Obrigatorio;
        r.Ordem = req.Ordem;
        return r;
    }

    private static string? Json(string[]? valores) =>
        valores is null || valores.Length == 0 ? null : JsonSerializer.Serialize(valores);

    private static string[] DeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static int? Inteiro(string v) => int.TryParse(v, out var n) ? n : null;

    private static string? Cortar(string? v, int max) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Length <= max ? v.Trim() : v[..max].Trim();

    private static RegulacaoRegraDto Mapear(RegulacaoRegra r) =>
        new(r.Id, r.ProcedimentoId, r.ProcedimentoOrigemId, r.Sistema, r.Tipo, r.Severidade,
            r.Descricao, r.Fonte, r.IdadeMinAnos, r.IdadeMaxAnos, r.Sexo, r.ExigeCpf,
            DeJson(r.CidsPermitidosJson), DeJson(r.CidsExcluidosJson),
            r.Pergunta, r.RespostaBloqueia, r.NaoSeiVira, r.DocumentoRotulo, r.TipoExameId,
            r.ValidadeDias, r.Obrigatorio, r.Ordem, r.Versao, r.Ativo, r.CriadoEm);
}
