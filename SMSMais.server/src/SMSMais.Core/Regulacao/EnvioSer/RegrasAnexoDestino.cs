using System.Text.Json;
using System.Text.Json.Nodes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>A regra de anexos de um destino, como a tela de configuração dele mostra.</summary>
/// <param name="TamanhoMaximoMb">Tamanho máximo por arquivo, em MB como o Windows mostra (1 MB =
/// 1.048.576 bytes), uma casa decimal. Acima disso o envio comprime o arquivo para caber.</param>
/// <param name="Configurado"><c>false</c> = ninguém configurou ainda; vale o padrão.</param>
public sealed record RegrasAnexoDestinoDto(decimal TamanhoMaximoMb, bool Configurado);

public sealed record SalvarRegrasAnexoDestinoRequest(decimal TamanhoMaximoMb);

/// <summary>
/// O tamanho máximo por arquivo que cada destino aceita no envio automático (SER e SERNIT), configurado
/// na aba do destino em vez de fixo no código: o "5 MB" que a tela do SER declara nunca foi medido
/// no servidor de lá, e cada destino pode ter o seu. Arquivo acima do limite é comprimido no envio
/// (<see cref="AnexosParaSer"/>).
///
/// <para>Mora no <c>ParametrosJson</c> da credencial do destino, como a varredura automática
/// (<see cref="SMSMais.Core.Ser.SerVarreduraConfigService"/>): o merge preserva as chaves que não são desta regra.</para>
/// </summary>
public interface IRegrasAnexoDestinoService
{
    Task<RegrasAnexoDestinoDto> ObterAsync(SistemaRegulacao sistema, CancellationToken ct);

    Task<RegrasAnexoDestinoDto> SalvarAsync(SistemaRegulacao sistema, decimal tamanhoMaximoMb, CancellationToken ct);

    /// <summary>O limite em bytes que o envio usa — o configurado, ou o padrão.</summary>
    Task<long> TamanhoMaximoBytesAsync(SistemaRegulacao sistema, CancellationToken ct);
}

public sealed class RegrasAnexoDestinoService(IIntegracaoCredencialService credenciais) : IRegrasAnexoDestinoService
{
    public const string ChaveTamanho = "anexoTamanhoMaximoBytes";
    public const decimal MinimoMb = 0.5m;
    public const decimal MaximoMb = 50m;
    private const decimal BytesPorMb = 1024m * 1024m;

    public async Task<RegrasAnexoDestinoDto> ObterAsync(SistemaRegulacao sistema, CancellationToken ct)
    {
        var configurado = LerBytes(await LerParametrosAsync(Provedor(sistema), ct));
        return new RegrasAnexoDestinoDto(EmMb(configurado ?? AnexosParaSer.TamanhoMaximoPadrao), configurado is not null);
    }

    public async Task<long> TamanhoMaximoBytesAsync(SistemaRegulacao sistema, CancellationToken ct) =>
        LerBytes(await LerParametrosAsync(Provedor(sistema), ct)) ?? AnexosParaSer.TamanhoMaximoPadrao;

    public async Task<RegrasAnexoDestinoDto> SalvarAsync(
        SistemaRegulacao sistema, decimal tamanhoMaximoMb, CancellationToken ct)
    {
        if (tamanhoMaximoMb < MinimoMb || tamanhoMaximoMb > MaximoMb)
        {
            throw new ValidacaoException(
                "anexos.tamanho",
                "O tamanho máximo por arquivo tem de ficar entre 0,5 e 50 MB.");
        }

        var provedor = Provedor(sistema);
        IntegracaoCredencialDto atual;
        try
        {
            atual = await credenciais.ObterAsync(provedor, ct);
        }
        catch (ValidacaoException)
        {
            throw new ValidacaoException(
                "anexos.sem_credencial",
                $"Cadastre a credencial do {NomeDe(sistema)} antes: a regra de anexos fica guardada junto dela.");
        }

        var bytes = (long)Math.Round(Math.Round(tamanhoMaximoMb, 1) * BytesPorMb);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[ChaveTamanho] = bytes;
        await credenciais.AtualizarAsync(
            provedor,
            new AtualizarIntegracaoCredencialRequest(
                // Vazios de propósito: o store trata vazio como "mantém o atual", e a senha do
                // destino é write-only — nunca passa por aqui.
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            ct);
        return new RegrasAnexoDestinoDto(EmMb(bytes), Configurado: true);
    }

    /// <summary>Só o SER e o SERNIT recebem arquivo da plataforma; o resto é "registrar envio".</summary>
    private static string Provedor(SistemaRegulacao sistema) => sistema switch
    {
        SistemaRegulacao.Ser => SerWebSessao.Provedor,
        SistemaRegulacao.Sernit => SernitWebSessao.Provedor,
        _ => throw new ValidacaoException(
            "anexos.destino", "Só o SER e o SERNIT recebem anexos enviados pela plataforma."),
    };

    private static string NomeDe(SistemaRegulacao sistema) => sistema == SistemaRegulacao.Sernit ? "SERNIT" : "SER";

    private static decimal EmMb(long bytes) => Math.Round(bytes / BytesPorMb, 1);

    /// <summary>O valor guardado, se for um número dentro da faixa — fora dela vale o padrão.</summary>
    private static long? LerBytes(JsonObject? json)
    {
        if (json?[ChaveTamanho] is not JsonValue valor || !valor.TryGetValue<long>(out var bytes)) return null;
        var mb = bytes / BytesPorMb;
        return mb >= MinimoMb && mb <= MaximoMb ? bytes : null;
    }

    private async Task<JsonObject?> LerParametrosAsync(string provedor, CancellationToken ct)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(provedor, ct);
            return Parse(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            // Destino ainda não cadastrado: vale o padrão.
            return null;
        }
    }

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
