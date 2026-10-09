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

    /// <summary>
    /// Campos dinâmicos que exigimos SEMPRE, mesmo quando o catálogo do recurso diz opcional
    /// (pedido do Bernardo, 01/10/2026): são o que o regulador lê para decidir. Medido no
    /// catálogo de produção nessa data — Observações vinha opcional em 16 recursos do SER e 56 do
    /// SERNIT, e o SERNIT escreve o resultado de exames de outro jeito em 53 recursos, opcional.
    /// </summary>
    private static readonly HashSet<string> SempreObrigatorios = new(StringComparer.Ordinal)
    {
        "queixa_principal",
        "resultado_de_exames",
        "principais_resultados_de_provas_diagnosticas_resultados_de_exames_realizados",
        "observacoes",
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

        // Os valores fixos do mapa entram no hash: trocar um deles (a unidade de origem, por
        // exemplo) não muda nenhum campo, e sem isto a versão antiga seria reaproveitada.
        var constantes = string.Join('|', mapa
            .Where(m => m.Transformacao?.StartsWith(PrefixoConstante, StringComparison.Ordinal) == true)
            .Select(m => $"{m.Sistema}:{m.NomeNativo}={m.Transformacao}"));
        var assinatura = constantes.Length == 0 ? $"{esquema}|{definicao}" : $"{esquema}|{definicao}|{constantes}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(assinatura)));

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
        var adicionados = new List<object> { versao };

        foreach (var m in mapa)
        {
            var linha = new RegulacaoFormularioCampoMapa
            {
                Id = Guid.CreateVersion7(),
                FormularioVersaoId = versao.Id,
                ChaveCanonica = m.Chave,
                Sistema = m.Sistema,
                NomeNativo = m.NomeNativo,
                Transformacao = m.Transformacao,
            };
            db.RegulacaoFormularioCampoMapas.Add(linha);
            adicionados.Add(linha);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Duas aberturas simultâneas do mesmo procedimento: o unique do hash faz a segunda
            // colidir. Quem perdeu usa a versão que a outra gravou — é a mesma definição.
            // Sai do rastreio só o que ESTE método acrescentou: limpar o rastreador inteiro
            // soltaria também a solicitação que quem chamou está editando, e a edição dela se
            // perderia calada no SaveChanges seguinte.
            foreach (var entidade in adicionados) db.Entry(entidade).State = EntityState.Detached;
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
                && (o.Sistema == SistemaRegulacao.Ser || o.Sistema == SistemaRegulacao.Sernit
                    || o.Sistema == SistemaRegulacao.EsusSg))
            .Select(o => new { o.Sistema, o.SerCatalogoRecursoId, o.SernitCatalogoRecursoId })
            .ToListAsync(ct);

        if (origens.Count == 0)
        {
            throw new ValidacaoException(
                "procedimento",
                "Este procedimento não tem oferta no SER, no SERNIT nem no ESUS de São Gonçalo — não há "
                + "formulário externo para ele.");
        }

        var brutos = new List<CampoBruto>();
        foreach (var o in origens)
        {
            // ESUS SG (ADR-0063): o formulário de inclusão na fila do ESUS não foi mapeado (a
            // integração é só leitura) — o pedido segue com os campos canônicos, sem bloco dinâmico.
            if (o.Sistema == SistemaRegulacao.EsusSg) continue;
            brutos.AddRange(o.Sistema == SistemaRegulacao.Ser
                ? await LerCamposSerAsync(o.SerCatalogoRecursoId, ct)
                : await LerCamposSernitAsync(o.SernitCatalogoRecursoId, ct));
        }

        var sistemasDoBloco = origens.Select(o => o.Sistema)
            .Where(s => s is SistemaRegulacao.Ser or SistemaRegulacao.Sernit).Distinct().ToList();

        foreach (var sistema in sistemasDoBloco)
        {
            brutos.AddRange(await CamposFixosAsync(sistema, ct));
        }

        var (campos, mapa) = Unir(brutos);

        // O que é decisão nossa, e não de quem pede, não vira campo na tela: vira linha no mapa
        // com o valor fixo. Médico vai pelo "Sim" (escolhido da lista do próprio sistema);
        // unidade de origem pelo "Não" + texto livre — a central reguladora do município não é
        // uma unidade que o SER sugira (ser-criar-solicitacao.md §2.1, "Os três radios").
        foreach (var sistema in sistemasDoBloco)
        {
            mapa.Add(new EntradaMapa(
                "medico_solicitante_identificado", sistema,
                "form0:booleanMedicoSolicitanteIdentificado_radio", $"{PrefixoConstante}true"));
            mapa.Add(new EntradaMapa(
                "unidade_origem_identificada", sistema,
                "form0:unidadeDeOrigemIdentificada_radio", $"{PrefixoConstante}false"));
            mapa.Add(new EntradaMapa(
                "unidade_origem", sistema,
                "form0:unidadeNaoIdentificada", $"{PrefixoConstante}{UnidadeOrigemTexto}"));
        }

        return (campos, mapa);
    }

    /// <summary>
    /// Transformação de linha do mapa que não lê o canônico: o nome nativo recebe sempre o valor
    /// depois do prefixo.
    /// </summary>
    private const string PrefixoConstante = "constante:";

    /// <summary>
    /// Médico solicitante escolhido da lista do próprio sistema (combo <c>medicoResp</c>, ~927
    /// no SER) — o caminho "Médico solicitante identificado? Sim". Guarda o <b>nome</b>.
    ///
    /// <para>Especialidade e telefone do médico NÃO são campos: o SER os preenche sozinho ao
    /// escolher o médico (inputs desabilitados, vindos da lotação dele no município — medido em
    /// 01/10/2026, <c>Automais.SER/probe_profissional_saude.py</c>).</para>
    /// </summary>
    public const string ChaveMedicoSolicitante = "medico_solicitante";

    /// <summary>
    /// O texto de "Unidade de origem" (radio "Não") — a central reguladora que pede.
    ///
    /// <para><b>Fixo no código POR ORA, por decisão do Bernardo (01/10/2026)</b>, contra a regra
    /// de nada institucional em código (ADR-0043): quando outra instância usar o SER/SERNIT, isto
    /// vira configuração da Regulação.</para>
    /// </summary>
    public const string UnidadeOrigemTexto = "CREGMARICA";

    /// <summary>Chave canônica da Classificação de risco do bloco fixo do SER/SERNIT.</summary>
    public const string ChaveClassificacaoRisco = "classificacao_risco";

    /// <summary>
    /// Chave canônica da Hipótese do bloco fixo. O valor é o texto do jeito que o SER escreve no
    /// campo — <c>(A09 ) Diarréia…</c> —, escolhido da lista de CID do recurso.
    /// </summary>
    public const string ChaveHipoteseCid = "hipotese_cid";

    /// <summary>
    /// O bloco <b>fixo</b> do SER/SERNIT — igual para todo recurso, e por isso fora do catálogo de
    /// campos dinâmicos que <see cref="LerCamposSerAsync"/> lê. Sem ele, a solicitação chegava à
    /// pré-regulação sem médico solicitante, Classificação de risco e Hipótese, que os dois
    /// sistemas exigem (docs/ser-criar-solicitacao.md §2.1). A unidade de origem não é campo: é
    /// fixa (<see cref="UnidadeOrigemTexto"/>) e vai pelo mapa.
    ///
    /// <para>A Hipótese é do tipo <c>cid</c>: no SER ela não é texto, é a caixa de CID do RECURSO
    /// (§2.1.3) — a tela busca em <c>GET regulacao/solicitacoes/formulario/cids</c>. Os nomes
    /// nativos são os mesmos nos dois sistemas (a mesma aplicação JSF).</para>
    ///
    /// <para>As listas (médicos, classificação de risco) são as copiadas do sistema pelo sync do
    /// catálogo. O médico de um sistema só vale nele: a união por rótulo marca a origem de cada
    /// opção, e a tela mostra só as do destino escolhido.</para>
    /// </summary>
    private async Task<List<CampoBruto>> CamposFixosAsync(SistemaRegulacao sistema, CancellationToken ct)
    {
        var riscos = await ListaAsync(sistema, "classificacao_risco", ct);
        // O value do combo de médico é o índice do EntityConverter do Seam — muda a cada view
        // (medido em 01/10/2026: 0..872 numa captura, fora de ordem em outra). Guardar o índice
        // mandaria OUTRO médico; o valor é o nome, e o envio resolve o índice pelo texto na hora.
        // Nome repetido no combo (homônimo ou cadastro duplicado) vira uma opção só.
        var medicos = (await ListaAsync(sistema, "medico", ct))
            .Select(m => m.Rotulo.Trim())
            .Where(nome => nome.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(nome => (nome, nome))
            .ToList();

        // Médicos pedidos na abertura e ainda não cadastrados no sistema: entram na lista para
        // outra unidade reaproveitar o pedido em vez de pedir de novo. O valor é `pendente:{id}`
        // — o "Registrar envio" barra enquanto o técnico não cadastrar e confirmar.
        var pendentes = await db.RegulacaoMedicosPendentes.AsNoTracking()
            .Where(m => m.Sistema == sistema && m.Situacao == SituacaoMedicoPendente.Pendente)
            .OrderBy(m => m.Nome)
            .Select(m => new { m.Id, m.Nome })
            .ToListAsync(ct);
        medicos.AddRange(pendentes.Select(m => (
            Medicos.RegulacaoMedicoPendenteService.Valor(m.Id),
            $"{m.Nome} (aguardando cadastro no {sistema.ToString().ToUpperInvariant()})")));

        // Ordem negativa: o bloco fixo vem antes do dinâmico, na ordem da tela do SER.
        return
        [
            new CampoBruto(
                ChaveMedicoSolicitante, "Médico solicitante", "select", true, medicos,
                sistema, "form0:medicoResp", -6),
            new CampoBruto(
                ChaveClassificacaoRisco, "Classificação de risco", "select", true, riscos,
                sistema, "form0:classificacao_risco", -3),
            new CampoBruto(
                ChaveHipoteseCid, "Hipótese (CID)", "cid", true, [],
                sistema, "form0:procedimento", -2),
        ];
    }

    /// <summary>Uma lista copiada do bloco fixo (<c>medico</c>, <c>classificacao_risco</c>).</summary>
    private async Task<List<(string Valor, string Rotulo)>> ListaAsync(
        SistemaRegulacao sistema, string lista, CancellationToken ct)
    {
        var linhas = sistema == SistemaRegulacao.Ser
            ? await db.SerCatalogoListas.AsNoTracking()
                .Where(l => l.Lista == lista)
                .OrderBy(l => l.Ordem)
                .Select(l => new { l.Valor, l.Rotulo })
                .ToListAsync(ct)
            : await db.SernitCatalogoListas.AsNoTracking()
                .Where(l => l.Lista == lista)
                .OrderBy(l => l.Ordem)
                .Select(l => new { l.Valor, l.Rotulo })
                .ToListAsync(ct);

        return [.. linhas.Select(l => (l.Valor, l.Rotulo))];
    }

    /// <summary>
    /// Pela linha do espelho que a origem guarda (id nosso), nunca pelo número do combo: o número
    /// é posição e a SES renumera (Regulacao.Catalogo.IdentidadePorNome) — lido pelo número, o
    /// formulário saía com os campos de outro recurso.
    /// </summary>
    private async Task<List<CampoBruto>> LerCamposSerAsync(Guid? recursoId, CancellationToken ct)
    {
        if (recursoId is null) return [];

        var campos = await serCatalogo.ObterCamposDoRecursoAsync(recursoId.Value, ct);

        return [.. campos.Select((c, i) => new CampoBruto(
            Slug(c.Rotulo), c.Rotulo, c.Tipo, c.Obrigatorio,
            [.. (c.Opcoes ?? []).Select(o => (o.Valor, o.Rotulo))],
            SistemaRegulacao.Ser, c.Campo, i))];
    }

    private async Task<List<CampoBruto>> LerCamposSernitAsync(Guid? recursoId, CancellationToken ct)
    {
        if (recursoId is null) return [];

        var campos = await sernitCatalogo.ObterCamposDoRecursoAsync(recursoId.Value, ct);

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
                        chave, $"{b.Rotulo} ({b.Sistema})", b.Tipo,
                        b.Obrigatorio || SempreObrigatorios.Contains(b.Chave),
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
            var obrigatorio = grupo.Any(b => b.Obrigatorio) || SempreObrigatorios.Contains(primeiro.Chave);

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

        var cidsSecundarios = canonico.TryGetProperty(ChaveCidsSecundarios, out var cs)
            ? ComoTexto(cs).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        foreach (var m in mapa)
        {
            if (m.Transformacao?.StartsWith(PrefixoConstante, StringComparison.Ordinal) == true)
            {
                saida[m.NomeNativo] = m.Transformacao[PrefixoConstante.Length..];
                continue;
            }

            var texto = canonico.TryGetProperty(m.ChaveCanonica, out var valor) ? ComoTexto(valor) : string.Empty;

            // Os sistemas só têm UM CID na tela: os secundários viajam no fim das Observações.
            if (ChavesObservacoes.Contains(m.ChaveCanonica)) texto = ComCidsSecundarios(texto, cidsSecundarios);

            if (string.IsNullOrEmpty(texto)) continue;

            saida[m.NomeNativo] = Transformar(texto, m.Transformacao);
        }
        return saida;
    }

    /// <summary>
    /// CIDs secundários da solicitação, um por linha. Não são campo de nenhum sistema: a tela do
    /// SER/SERNIT/SISREG tem um CID só, e os demais vão no fim das Observações.
    /// </summary>
    public const string ChaveCidsSecundarios = "cids_secundarios";

    private static readonly HashSet<string> ChavesObservacoes = ["observacoes", "observacao"];

    /// <summary>As Observações como vão ao sistema — espelha `observacoesComCids` do front.</summary>
    public static string ComCidsSecundarios(string? observacoes, IReadOnlyCollection<string> cids)
    {
        var baseTexto = (observacoes ?? string.Empty).Trim();
        if (cids.Count == 0) return baseTexto;
        var linha = $"CID(s) secundário(s): {string.Join("; ", cids)}";
        return baseTexto.Length == 0 ? linha : $"{baseTexto}\n\n{linha}";
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
