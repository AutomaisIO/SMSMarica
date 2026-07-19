using System.Security.Cryptography;
using System.Text;
using Automais.Pabx.Api.Asterisk;
using Automais.Pabx.Api.Data;
using Automais.Pabx.Api.Data.Entities;
using Automais.Pabx.Api.Infra.Excecoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Automais.Pabx.Api.Provisionamento;

public interface IProvisionamentoService
{
    /// <summary>
    /// Falha com 409 se o arquivo-alvo do MAC já existe no TFTP e não é gerenciado por nós.
    /// Chamar ANTES de persistir o ramal, para não deixar inventário meio-criado.
    /// </summary>
    Task VerificarDisponibilidadeAsync(MarcaTelefone marca, string mac, CancellationToken ct = default);

    /// <summary>Gera (ou regenera) o arquivo de auto-provisionamento do aparelho no TFTP.</summary>
    Task<string> GerarAsync(Ramal ramal, string secret, CancellationToken ct = default);

    /// <summary>Remove o arquivo de provisionamento do ramal, se gerenciado por nós.</summary>
    Task RemoverAsync(Ramal ramal, CancellationToken ct = default);
}

/// <summary>
/// Publica XML de auto-provisionamento em /var/lib/tftpboot (TFTP já existente no servidor).
/// Templates copiados dos arquivos reais em produção. O diretório é compartilhado com
/// outros clientes da FalarMais: só tocamos arquivos registrados em ArquivoGerenciado.
/// </summary>
public sealed class ProvisionamentoService(
    PabxDbContext db,
    IOptions<AsteriskOptions> options,
    TimeProvider timeProvider,
    ILogger<ProvisionamentoService> logger) : IProvisionamentoService
{
    private readonly AsteriskOptions _opcoes = options.Value;

    public async Task VerificarDisponibilidadeAsync(MarcaTelefone marca, string mac, CancellationToken ct = default)
    {
        var nomeArquivo = NomeArquivo(marca, mac);
        var caminho = Path.Combine(_opcoes.TftpDir, nomeArquivo);
        var gerenciado = await db.ArquivosGerenciados.AnyAsync(a => a.Caminho == caminho, ct);
        if (!gerenciado && File.Exists(caminho))
            throw new ConflitoException("arquivo_alheio",
                $"Já existe {nomeArquivo} no TFTP e ele não foi criado por este serviço — o servidor é " +
                "compartilhado; confira o MAC ou trate o arquivo existente manualmente.");
    }

    public async Task<string> GerarAsync(Ramal ramal, string secret, CancellationToken ct = default)
    {
        if (ramal.Mac is null || ramal.Marca is null)
            throw new ValidacaoException("mac", "Ramal sem MAC/marca cadastrados: nada a provisionar.");

        await VerificarDisponibilidadeAsync(ramal.Marca.Value, ramal.Mac, ct);

        var nomeArquivo = NomeArquivo(ramal.Marca.Value, ramal.Mac);
        var caminho = Path.Combine(_opcoes.TftpDir, nomeArquivo);
        var registro = await db.ArquivosGerenciados.FirstOrDefaultAsync(a => a.Caminho == caminho, ct);

        var conteudo = MontarConteudo(ramal, secret);
        await ArquivoSeguro.EscreverComBackupAsync(caminho, conteudo, _opcoes.BackupDir, timeProvider, ct);

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo)));
        if (registro is null)
        {
            registro = new ArquivoGerenciado
            {
                Caminho = caminho,
                HashSha256 = hash,
                Tipo = TipoArquivoGerenciado.ProvisionamentoXml,
                RamalId = ramal.Id,
            };
            db.ArquivosGerenciados.Add(registro);
        }

        registro.HashSha256 = hash;
        registro.RamalId = ramal.Id;
        registro.AtualizadoEm = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Provisionamento gerado: {Arquivo} (ramal {Numero}, {Marca} {Modelo})",
            nomeArquivo, ramal.Numero, ramal.Marca, ramal.Modelo);
        return caminho;
    }

    public async Task RemoverAsync(Ramal ramal, CancellationToken ct = default)
    {
        var registros = await db.ArquivosGerenciados
            .Where(a => a.RamalId == ramal.Id && a.Tipo == TipoArquivoGerenciado.ProvisionamentoXml)
            .ToListAsync(ct);

        foreach (var registro in registros)
        {
            if (File.Exists(registro.Caminho))
                File.Delete(registro.Caminho);
            db.ArquivosGerenciados.Remove(registro);
            logger.LogInformation("Provisionamento removido: {Arquivo}", registro.Caminho);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Convenção de nome que cada fabricante busca no TFTP ao ligar.</summary>
    public static string NomeArquivo(MarcaTelefone marca, string mac) => marca switch
    {
        MarcaTelefone.Intelbras => $"{mac.ToUpperInvariant()}.xml",
        MarcaTelefone.Cisco => $"SEP{mac.ToUpperInvariant()}.cnf.xml",
        MarcaTelefone.Grandstream => $"cfg{mac.ToLowerInvariant()}.xml",
        _ => throw new ArgumentOutOfRangeException(nameof(marca)),
    };

    private string MontarConteudo(Ramal ramal, string secret)
    {
        var template = CarregarTemplate(ramal.Marca!.Value);
        var modelo = string.IsNullOrWhiteSpace(ramal.Modelo) ? ModeloPadrao(ramal.Marca.Value) : ramal.Modelo!;

        return template
            .Replace("{{RAMAL}}", ramal.Numero)
            .Replace("{{SECRET}}", secret)
            .Replace("{{SERVIDOR}}", _opcoes.SipServerParaTelefones)
            .Replace("{{MODELO}}", modelo)
            .Replace("{{LABEL}}", MontarLabel(ramal));
    }

    /// <summary>Cisco 3905 limita o label a 13 caracteres sem espaços.</summary>
    private static string MontarLabel(Ramal ramal)
    {
        var texto = string.IsNullOrWhiteSpace(ramal.Descricao) ? ramal.Numero : ramal.Descricao!;
        var limpo = new string([.. texto.Where(char.IsAsciiLetterOrDigit)]);
        if (limpo.Length == 0)
            limpo = ramal.Numero;
        return limpo.Length <= 13 ? limpo : limpo[..13];
    }

    private static string ModeloPadrao(MarcaTelefone marca) => marca switch
    {
        MarcaTelefone.Intelbras => "TIP125",
        MarcaTelefone.Cisco => "3905",
        MarcaTelefone.Grandstream => "GXP1610",
        _ => "",
    };

    private static string CarregarTemplate(MarcaTelefone marca)
    {
        var nome = marca switch
        {
            MarcaTelefone.Intelbras => "intelbras-tip.xml",
            MarcaTelefone.Cisco => "cisco-3905.xml",
            MarcaTelefone.Grandstream => "grandstream-gxp.xml",
            _ => throw new ArgumentOutOfRangeException(nameof(marca)),
        };

        var caminho = Path.Combine(AppContext.BaseDirectory, "Provisionamento", "Templates", nome);
        return File.ReadAllText(caminho);
    }
}
