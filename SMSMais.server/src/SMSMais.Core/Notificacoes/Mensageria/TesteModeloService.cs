using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Conversas;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Core.Notificacoes.Mensageria;

/// <summary>Modelo aprovado na Meta, com o corpo e os exemplos para a tela de teste.</summary>
public sealed record ModeloWhatsAppDto(
    string Nome, string Idioma, string Categoria, string? Corpo, int Parametros,
    IReadOnlyList<string> Exemplos,
    /// <summary>Variáveis na ordem: ["1","2"] (numerado) ou ["nome","data"] (nomeado). A Meta
    /// recusa (132012) quem envia no formato diferente do que o modelo foi criado.</summary>
    IReadOnlyList<string> Variaveis,
    bool Nomeadas,
    /// <summary>Parâmetros que o SISTEMA montaria para este modelo (quando ele é um dos nossos) —
    /// é o que faz o teste valer como ensaio do envio real, e não um texto qualquer.</summary>
    IReadOnlyList<string> Sugestao,
    /// <summary>Formato do cabeçalho aprovado na Meta: IMAGE, VIDEO, DOCUMENT, TEXT — ou nulo
    /// quando o modelo não tem cabeçalho (ou o relay ainda não informa).</summary>
    string? CabecalhoFormato = null,
    /// <summary>O modelo tem mídia no topo, e portanto EXIGE a arte em todo envio.</summary>
    bool CabecalhoExigeArte = false,
    /// <summary>Texto do cabeçalho, quando ele é de texto.</summary>
    string? CabecalhoTexto = null,
    /// <summary>A arte em vigor — a mesma que o envio usaria. Nula = nenhuma definida.</summary>
    string? ArteUrl = null,
    /// <summary>A arte vem do Automais.Zap, que é onde ela se publica e se troca.</summary>
    bool ArteNaPlataforma = false)
{
    /// <summary>
    /// Exige arte e não tem nenhuma: o envio deste modelo é recusado antes de chegar à Meta.
    /// É o aviso que a tela precisa mostrar em vermelho.
    /// </summary>
    public bool ArtePendente => CabecalhoExigeArte && string.IsNullOrWhiteSpace(ArteUrl);
}

public sealed record ResultadoTesteModeloDto(bool Ok, string? Erro, string? WaMessageId);

/// <summary>
/// Disparo de UM modelo para UM número, para conferir na tela do celular como a mensagem chega —
/// texto, botões e variáveis. Existe porque não havia onde testar: "Nova conversa" só libera um
/// modelo e o Sandbox só manda texto livre, que exige janela de 24h aberta.
///
/// <para>É envio de gente (<see cref="OrigemEnvioWhatsApp.Humano"/>): não passa por fila, não cria
/// comunicação e não mexe em agendamento nenhum. A guarda de contato negado por número continua
/// valendo.</para>
/// </summary>
public interface ITesteModeloService
{
    Task<IReadOnlyList<ModeloWhatsAppDto>> ListarModelosAsync(CancellationToken ct = default);

    Task<ResultadoTesteModeloDto> EnviarAsync(
        string telefone, string modelo, IReadOnlyList<string>? parametros, CancellationToken ct = default);
}

public sealed class TesteModeloService(
    IWhatsAppCliente whatsApp,
    Microsoft.Extensions.Options.IOptions<ComunicacaoPacienteOptions> options) : ITesteModeloService
{
    public async Task<IReadOnlyList<ModeloWhatsAppDto>> ListarModelosAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        var catalogo = await whatsApp.ListarTemplatesAsync(ct);

        return [.. catalogo.Select(t =>
        {
            // A arte é do Automais.Zap; o mapa de configuração só cobre o período em que o relay
            // ainda não a informa. Aqui é leitura — trocar a imagem é lá.
            var daPlataforma = t.Cabecalho?.Arte;
            var arte = daPlataforma
                       ?? (opts.ImagensCabecalho is not null
                           && opts.ImagensCabecalho.TryGetValue(t.Nome, out var padrao)
                           && !string.IsNullOrWhiteSpace(padrao)
                           ? padrao
                           : null);

            return new ModeloWhatsAppDto(
                t.Nome, t.Idioma, t.Categoria, t.Corpo, t.Parametros, t.Exemplos,
                t.Variaveis ?? [], t.Nomeadas,
                SugestaoDe(t.Nome, t.Parametros, opts),
                t.Cabecalho?.Formato,
                t.Cabecalho?.ExigeMidia ?? false,
                t.Cabecalho?.Texto,
                arte,
                daPlataforma is not null);
        })];
    }

    public async Task<ResultadoTesteModeloDto> EnviarAsync(
        string telefone, string modelo, IReadOnlyList<string>? parametros, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelo))
            throw new ValidacaoException("teste.modelo_obrigatorio", "Escolha o modelo a enviar.");

        var fone = TelefoneWhatsApp.Canonizar(telefone ?? string.Empty);
        if (!TelefoneWhatsApp.EhCelularBr(fone))
            throw new ValidacaoException("teste.telefone_invalido", "Informe um celular com DDD.");
        fone = TelefoneWhatsApp.NormalizarNonoDigito(fone);

        var opts = options.Value;
        var catalogo = await whatsApp.ListarTemplatesAsync(ct);
        var aprovado = catalogo.FirstOrDefault(t =>
            string.Equals(t.Nome, modelo, StringComparison.OrdinalIgnoreCase));

        // Quantas variáveis o modelo pede manda no que se envia: a mais é erro 132000 da Meta.
        var esperados = aprovado?.Parametros ?? parametros?.Count ?? 0;
        var valores = (parametros is { Count: > 0 } ? parametros : SugestaoDe(modelo, esperados, opts)).ToList();
        if (valores.Count > esperados) valores = [.. valores.Take(esperados)];
        while (valores.Count < esperados) valores.Add($"teste {valores.Count + 1}");

        var envio = await whatsApp.EnviarTemplateAsync(
            fone, aprovado?.Nome ?? modelo, aprovado?.Idioma ?? opts.Idioma, valores,
            pacienteId: null, conteudoLegivel: null, ct: ct,
            origem: OrigemEnvioWhatsApp.Humano);

        return new ResultadoTesteModeloDto(envio.Ok, envio.Erro, envio.WaMessageId);
    }

    /// <summary>
    /// Os valores que o sistema usaria de verdade, para o teste ensaiar o envio real. Modelo que
    /// não é nosso (ou que mudou de forma) cai no genérico "exemplo 1, 2, 3…".
    /// </summary>
    private static string[] SugestaoDe(string modelo, int parametros, ComunicacaoPacienteOptions opts)
    {
        var amanha = Common.Tempo.FusoBrasilia.ParaExibicao(DateTime.UtcNow.AddDays(2));
        var data = amanha.ToString("dd/MM/yyyy");

        if (Igual(modelo, opts.TemplateConfirmacaoExame)) return ["Sr. Teste"];
        if (Igual(modelo, opts.TemplateConfirmacaoConsulta)) return ["Sra. Teste"];
        if (Igual(modelo, opts.TemplateLembreteConfirmado))
            return ["Sr. Teste", "Seu exame de Mamografia", data, "14:00h"];
        if (Igual(modelo, opts.TemplateExameLiberado) || Igual(modelo, opts.TemplateLaudoPronto))
            return ["Teste", "Mamografia", data];
        if (Igual(modelo, opts.TemplateConfirmaAgendamento))
            return
            [
                "Sr. Teste", "O seu exame", "Mamografia", $"{data} às 14:00h",
                "o Sr. é assistido", "do seu exame", "o Sr.",
            ];

        return [.. Enumerable.Range(1, Math.Max(0, parametros)).Select(i => $"exemplo {i}")];
    }

    private static bool Igual(string a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
