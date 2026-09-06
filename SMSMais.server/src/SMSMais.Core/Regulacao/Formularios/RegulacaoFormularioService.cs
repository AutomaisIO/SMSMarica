using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Ser;
using SMSMais.Core.Sernit;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Regulacao.Formularios;

public sealed record OpcaoFormularioDto(
    string Valor,
    string Rotulo,
    IReadOnlyList<SistemaRegulacao> Origens);

public sealed record CampoFormularioDto(
    string Chave,
    string Rotulo,
    string Tipo,
    bool Obrigatorio,
    IReadOnlyList<OpcaoFormularioDto>? Opcoes,
    IReadOnlyList<SistemaRegulacao> Origens,
    int Ordem);

public sealed record RegulacaoFormularioDto(
    Guid VersaoId,
    string Esquema,
    IReadOnlyList<CampoFormularioDto> Campos);

public interface IRegulacaoFormularioService
{
    /// <summary>Monta (ou reusa, pelo hash) a versão do formulário daquele procedimento e fluxo.</summary>
    Task<RegulacaoFormularioDto> ObterOuGerarAsync(
        Guid procedimentoId, FluxoRegulacao fluxo, CancellationToken ct);

    /// <summary>Converte o preenchimento canônico para os nomes e formatos de um sistema.</summary>
    Task<IReadOnlyDictionary<string, string>> TraduzirAsync(
        Guid formularioVersaoId, SistemaRegulacao sistema, JsonElement canonico, CancellationToken ct);

    IReadOnlyList<string> ObrigatoriosFaltando(RegulacaoFormularioDto formulario, JsonElement canonico);
}

/// <summary>
/// Gera o formulário que o solicitante preenche (plano 02).
///
/// <para><b>Externo é a união do SER com o SERNIT</b>, não o menor denominador: campo que existe
/// de um lado só entra mesmo assim, e obrigatoriedade é <b>OU</b>. A razão é medida — no spike c,
/// dos 23 pares, o lado SER traz sempre o mesmo molde de 3 campos e o SERNIT traz 7 ou 12.
/// Preencher pelo menor faria a solicitação ser recusada no destino mais exigente.</para>
///
/// <para><b>Conflito não vira regra automática.</b> Tipo diferente entre os sistemas gera dois
/// campos sufixados, em vez de o sistema escolher um: nos 23 pares medidos não houve <b>nenhum</b>
/// conflito de tipo nem de opções, então inventar a regra agora seria adivinhação — e adivinhar
/// errado aqui manda dado no campo errado do sistema de destino.</para>
/// </summary>
public sealed class RegulacaoFormularioService(
    SmsMaisDbContext db,
    ISerCatalogoService serCatalogo,
    ISernitCatalogoService sernitCatalogo) : IRegulacaoFormularioService
{
    private const string EsquemaExterno = "externo.uniao";
    private const string EsquemaInterno = "sisreg.inclusao";

    /// <summary>
    /// Rótulos que os dois sistemas escrevem diferente e significam a mesma coisa. Semente do
    /// spike c: sem isto, o par `VIDEOLARINGOSCOPIA` fica com <b>zero</b> campo em comum só
    /// porque um escreve "Observações" e o outro "Observação".
    /// </summary>
    private static readonly Dictionary<string, string> SinonimosDeRotulo = new(StringComparer.Ordinal)
    {
        ["observacao"] = "observacoes",
    };

    private static readonly JsonSerializerOptions JsonCompacto = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<RegulacaoFormularioDto> ObterOuGerarAsync(
        Guid procedimentoId, FluxoRegulacao fluxo, CancellationToken ct)
    {
        var esquema = fluxo == FluxoRegulacao.Externo ? EsquemaExterno : EsquemaInterno;

        var (campos, mapa) = esquema == EsquemaExterno
            ? await MontarExternoAsync(procedimentoId, ct)
            : MontarInterno();

        var definicao = JsonSerializer.Serialize(campos, JsonCompacto);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{esquema}|{definicao}")));

        // Mesmo conjunto de campos ⇒ mesma linha. Sem isto, cada abertura de solicitação criaria
        // uma versão nova idêntica e o histórico viraria ruído.
        var existente = await db.RegulacaoFormularioVersoes.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Hash == hash, ct);
        if (existente is not null) return new RegulacaoFormularioDto(existente.Id, esquema, campos);

        var versao = new RegulacaoFormularioVersao
        {
            Id = Guid.CreateVersion7(),
            Esquema = esquema,
            ProcedimentoId = procedimentoId,
            DefinicaoJson = definicao,
            Hash = hash,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoFormularioVersoes.Add(versao);

        foreach (var m in mapa)
        {
            db.RegulacaoFormularioCampoMapas.Add(new RegulacaoFormularioCampoMapa
            {
                Id = Guid.CreateVersion7(),
                FormularioVersaoId = versao.Id,
                ChaveCanonica = m.Chave,
                Sistema = m.Sistema,
                NomeNativo = m.NomeNativo,
                Transformacao = m.Transformacao,
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Duas aberturas simultâneas do mesmo procedimento: o unique do hash faz a segunda
            // colidir. Quem perdeu usa a versão que a outra gravou — é a mesma definição.
            db.ChangeTracker.Clear();
            var deOutro = await db.RegulacaoFormularioVersoes.AsNoTracking()
                .FirstAsync(v => v.Hash == hash, ct);
            return new RegulacaoFormularioDto(deOutro.Id, esquema, campos);
        }

        return new RegulacaoFormularioDto(versao.Id, esquema, campos);
    }

    // ---------------------------------------------------------------- externo

    private sealed record EntradaMapa(string Chave, SistemaRegulacao Sistema, string NomeNativo, string? Transformacao);

    private sealed record CampoBruto(
        string Chave, string Rotulo, string Tipo, bool Obrigatorio,
        IReadOnlyList<(string Valor, string Rotulo)> Opcoes,
        SistemaRegulacao Sistema, string NomeNativo, int Ordem);

    private async Task<(List<CampoFormularioDto> Campos, List<EntradaMapa> Mapa)> MontarExternoAsync(
        Guid procedimentoId, CancellationToken ct)
    {
        var origens = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.ProcedimentoId == procedimentoId && o.Ativo
                && (o.Sistema == SistemaRegulacao.Ser || o.Sistema == SistemaRegulacao.Sernit))
            .Select(o => new { o.Sistema, o.ChaveExterna, o.Ramo })
            .ToListAsync(ct);

        if (origens.Count == 0)
        {
            throw new ValidacaoException(
                "procedimento",
                "Este procedimento não tem oferta no SER nem no SERNIT — não há formulário externo para ele.");
        }

        var brutos = new List<CampoBruto>();
        foreach (var o in origens)
        {
            brutos.AddRange(o.Sistema == SistemaRegulacao.Ser
                ? await LerCamposSerAsync(o.ChaveExterna, o.Ramo, ct)
                : await LerCamposSernitAsync(o.ChaveExterna, ct));
        }

        return Unir(brutos);
    }

    private async Task<List<CampoBruto>> LerCamposSerAsync(string chaveExterna, string? ramo, CancellationToken ct)
    {
        // Chave externa do SER: "{tipo}|{valor}|{AE|NAO_AE}".
        var partes = chaveExterna.Split('|');
        if (partes.Length < 2 || !int.TryParse(partes[0], out var tipo)) return [];

        var campos = await serCatalogo.ObterCamposAsync(
            (TipoRecursoSer)tipo, partes[1], ramo == "AE", ct);

        return [.. campos.Select((c, i) => new CampoBruto(
            Slug(c.Rotulo), c.Rotulo, c.Tipo, c.Obrigatorio,
            [.. (c.Opcoes ?? []).Select(o => (o.Valor, o.Rotulo))],
            SistemaRegulacao.Ser, c.Campo, i))];
    }

    private async Task<List<CampoBruto>> LerCamposSernitAsync(string chaveExterna, CancellationToken ct)
    {
        var partes = chaveExterna.Split('|');
        if (partes.Length < 2 || !int.TryParse(partes[0], out var tipo)) return [];

        var campos = await sernitCatalogo.ObterCamposAsync(
            (TipoRecursoSernit)tipo, partes[1], ct);

        return [.. campos.Select((c, i) => new CampoBruto(
            Slug(c.Rotulo), c.Rotulo, c.Tipo, c.Obrigatorio,
            [.. (c.Opcoes ?? []).Select(o => (o.Valor, o.Rotulo))],
            SistemaRegulacao.Sernit, c.Campo, i))];
    }

    private static (List<CampoFormularioDto> Campos, List<EntradaMapa> Mapa) Unir(List<CampoBruto> brutos)
    {
        var campos = new List<CampoFormularioDto>();
        var mapa = new List<EntradaMapa>();

        foreach (var grupo in brutos.GroupBy(b => b.Chave).OrderBy(g => g.Min(b => b.Ordem)))
        {
            // Tipos diferentes entre sistemas: NÃO se escolhe um. Vira um campo por sistema,
            // sufixado, e a curadoria resolve (spike c: zero conflitos medidos, então não há
            // caso real para calibrar uma regra automática).
            var tipos = grupo.Select(b => b.Tipo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (tipos.Count > 1)
            {
                foreach (var b in grupo)
                {
                    var chave = $"{b.Chave}_{b.Sistema.ToString().ToLowerInvariant()}";
                    campos.Add(new CampoFormularioDto(
                        chave, $"{b.Rotulo} ({b.Sistema})", b.Tipo, b.Obrigatorio,
                        Opcoes(b), [b.Sistema], campos.Count));
                    mapa.Add(new EntradaMapa(chave, b.Sistema, b.NomeNativo, TransformacaoDe(b.Tipo)));
                }
                continue;
            }

            var primeiro = grupo.First();
            var sistemas = grupo.Select(b => b.Sistema).Distinct().OrderBy(s => s).ToList();
            var opcoesUnidas = UnirOpcoes(grupo);

            // Obrigatoriedade é OU: exigir é o lado seguro. No spike c a única divergência
            // medida foi "Observações" (obrigatório no SER, opcional no SERNIT), e o SERNIT
            // aceita o campo preenchido sem reclamar.
            var obrigatorio = grupo.Any(b => b.Obrigatorio);

            campos.Add(new CampoFormularioDto(
                primeiro.Chave, primeiro.Rotulo, primeiro.Tipo, obrigatorio,
                opcoesUnidas, sistemas, campos.Count));

            foreach (var b in grupo)
            {
                mapa.Add(new EntradaMapa(
                    primeiro.Chave, b.Sistema, b.NomeNativo, TransformacaoDe(b.Tipo, opcoesUnidas, b)));
            }
        }

        return (campos, mapa);
    }

    private static IReadOnlyList<OpcaoFormularioDto>? Opcoes(CampoBruto b) =>
        b.Opcoes.Count == 0 ? null : [.. b.Opcoes.Select(o => new OpcaoFormularioDto(o.Valor, o.Rotulo, [b.Sistema]))];

    /// <summary>
    /// Opções unidas <b>por rótulo</b>, não por valor: o `valor` do combo é interno de cada
    /// instância e não bate entre sistemas (spike c). Quando o mesmo rótulo tem valores
    /// diferentes, o de-para vai para a transformação do mapa.
    /// </summary>
    private static IReadOnlyList<OpcaoFormularioDto>? UnirOpcoes(IEnumerable<CampoBruto> grupo)
    {
        var porRotulo = new Dictionary<string, (string Valor, string Rotulo, List<SistemaRegulacao> Origens)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var b in grupo)
        {
            foreach (var (valor, rotulo) in b.Opcoes)
            {
                if (porRotulo.TryGetValue(rotulo, out var atual)) atual.Origens.Add(b.Sistema);
                else porRotulo[rotulo] = (valor, rotulo, [b.Sistema]);
            }
        }

        return porRotulo.Count == 0
            ? null
            : [.. porRotulo.Values.Select(v => new OpcaoFormularioDto(v.Valor, v.Rotulo, v.Origens))];
    }

    private static string? TransformacaoDe(string tipo) =>
        tipo.Equals("date", StringComparison.OrdinalIgnoreCase) ? "data_ddMMyyyy"
        : tipo.Equals("checkbox", StringComparison.OrdinalIgnoreCase) ? "multiplo_quebra_linha"
        : null;

    /// <summary>
    /// De-para de opções para UM sistema.
    ///
    /// <para>O canônico guarda o valor do <b>primeiro</b> sistema (é o que <c>UnirOpcoes</c>
    /// escolhe). Quando o outro sistema usa outro código para o mesmo rótulo, o envio precisa
    /// traduzir — o `valor` do combo é interno de cada instância e não bate entre sistemas
    /// (medido no spike c). Sem esta tradução, o valor iria como o código do sistema errado e o
    /// destino recusaria a solicitação com o campo aparentemente preenchido.</para>
    /// </summary>
    private static string? TransformacaoDe(
        string tipo, IReadOnlyList<OpcaoFormularioDto>? canonicas, CampoBruto atual)
    {
        var basica = TransformacaoDe(tipo);
        if (canonicas is null || atual.Opcoes.Count == 0) return basica;

        var dePara = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var canonica in canonicas)
        {
            var noSistema = atual.Opcoes.FirstOrDefault(o =>
                string.Equals(o.Rotulo, canonica.Rotulo, StringComparison.OrdinalIgnoreCase));

            if (noSistema.Valor is not null && noSistema.Valor != canonica.Valor)
            {
                dePara[canonica.Valor] = noSistema.Valor;
            }
        }

        return dePara.Count > 0
            ? $"opcao:{JsonSerializer.Serialize(dePara, JsonCompacto)}"
            : basica;
    }

    // ---------------------------------------------------------------- interno

    /// <summary>
    /// Formulário do SISREG. <b>Provisório</b>: os campos reais da tela `marcar` só se conhecem
    /// depois do spike b, que escreve em sistema real e depende de OK. Estes cinco são os que o
    /// mapa por GET já documenta em <c>Automais.SISREG/docs/APRENDIZADOS.md</c>.
    /// </summary>
    private static (List<CampoFormularioDto> Campos, List<EntradaMapa> Mapa) MontarInterno()
    {
        List<CampoFormularioDto> campos =
        [
            new("cid10", "CID", "cid", false, null, [SistemaRegulacao.Sisreg], 0),
            new("profissional_solicitante_cpf", "CPF do profissional solicitante", "texto", true, null, [SistemaRegulacao.Sisreg], 1),
            new("profissional_solicitante_nome", "Nome do profissional solicitante", "texto", true, null, [SistemaRegulacao.Sisreg], 2),
            new("retorno", "É retorno?", "radio",
                false,
                [new OpcaoFormularioDto("S", "Sim", [SistemaRegulacao.Sisreg]),
                 new OpcaoFormularioDto("N", "Não", [SistemaRegulacao.Sisreg])],
                [SistemaRegulacao.Sisreg], 3),
            new("observacao", "Observação", "textarea", false, null, [SistemaRegulacao.Sisreg], 4),
        ];

        List<EntradaMapa> mapa =
        [
            new("cid10", SistemaRegulacao.Sisreg, "cid10", null),
            new("profissional_solicitante_cpf", SistemaRegulacao.Sisreg, "cpfprofsol", null),
            new("profissional_solicitante_nome", SistemaRegulacao.Sisreg, "nomeprofsol", null),
            new("retorno", SistemaRegulacao.Sisreg, "ret", null),
            new("observacao", SistemaRegulacao.Sisreg, "observacao", null),
        ];

        return (campos, mapa);
    }

    // ---------------------------------------------------------------- tradução

    public async Task<IReadOnlyDictionary<string, string>> TraduzirAsync(
        Guid formularioVersaoId, SistemaRegulacao sistema, JsonElement canonico, CancellationToken ct)
    {
        var mapa = await db.RegulacaoFormularioCampoMapas.AsNoTracking()
            .Where(m => m.FormularioVersaoId == formularioVersaoId && m.Sistema == sistema)
            .ToListAsync(ct);

        var saida = new Dictionary<string, string>(StringComparer.Ordinal);
        if (canonico.ValueKind != JsonValueKind.Object) return saida;

        foreach (var m in mapa)
        {
            if (!canonico.TryGetProperty(m.ChaveCanonica, out var valor)) continue;

            var texto = ComoTexto(valor);
            if (string.IsNullOrEmpty(texto)) continue;

            saida[m.NomeNativo] = Transformar(texto, m.Transformacao);
        }
        return saida;
    }

    private static string ComoTexto(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? string.Empty,
        JsonValueKind.Number => v.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        // Múltipla escolha chega como lista e viaja junta, separada por quebra de linha — é o
        // contrato que `SerValorMultiplo` desdobra na hora do envio.
        JsonValueKind.Array => string.Join('\n', v.EnumerateArray().Select(ComoTexto).Where(s => s.Length > 0)),
        _ => string.Empty,
    };

    private static string Transformar(string valor, string? transformacao)
    {
        if (string.IsNullOrEmpty(transformacao)) return valor;

        if (transformacao == "data_ddMMyyyy")
        {
            // O canônico guarda ISO; os dois sistemas falam dd/MM/yyyy no rich:calendar.
            return DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var d)
                ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : valor;
        }

        if (transformacao == "multiplo_quebra_linha") return valor;

        if (transformacao.StartsWith("opcao:", StringComparison.Ordinal))
        {
            try
            {
                var dePara = JsonSerializer.Deserialize<Dictionary<string, string>>(transformacao[6..]);
                if (dePara is not null && dePara.TryGetValue(valor, out var traduzido)) return traduzido;
            }
            catch (JsonException)
            {
                // De-para corrompido não pode derrubar o envio: segue com o valor original e o
                // sistema de destino recusa com mensagem, que é diagnóstico melhor do que 500.
            }
        }

        return valor;
    }

    public IReadOnlyList<string> ObrigatoriosFaltando(
        RegulacaoFormularioDto formulario, JsonElement canonico)
    {
        var faltando = new List<string>();
        foreach (var c in formulario.Campos.Where(c => c.Obrigatorio))
        {
            if (canonico.ValueKind != JsonValueKind.Object
                || !canonico.TryGetProperty(c.Chave, out var v)
                || string.IsNullOrWhiteSpace(ComoTexto(v)))
            {
                faltando.Add(c.Chave);
            }
        }
        return faltando;
    }

    // ---------------------------------------------------------------- apoio

    /// <summary>
    /// Chave canônica = rótulo sem acento, minúsculo, com `_`. Passa pela tabela de sinônimos:
    /// "Observação" e "Observações" têm de virar a mesma chave, senão a união duplica o campo.
    /// </summary>
    internal static string Slug(string rotulo)
    {
        var normalizado = (rotulo ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);
        var espacoPendente = false;

        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                if (espacoPendente && sb.Length > 0) sb.Append('_');
                espacoPendente = false;
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                espacoPendente = true;
            }
        }

        var slug = sb.ToString();
        return SinonimosDeRotulo.TryGetValue(slug, out var canonico) ? canonico : slug;
    }
}
