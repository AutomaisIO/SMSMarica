using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Identidade;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Core.Inteligencia.Configuracao;

/// <summary>
/// Gerencia a linha única de configuração global do módulo IA (provedor/modelo de geração
/// e de embeddings + tokens cifrados). Os tokens são write-only: a API nunca os devolve,
/// apenas sinaliza se estão definidos.
/// </summary>
public sealed class IaConfiguracaoService(
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    IUsuarioAtualAccessor usuarioAtual) : IIaConfiguracaoService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IProtetorSegredos _protetor = protetor;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<ConfiguracaoDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var config = await ObterOuCriarAsync(cancellationToken);
        return ParaDto(config);
    }

    public async Task AtualizarAsync(AtualizarConfiguracaoRequest request, CancellationToken cancellationToken = default)
    {
        var config = await ObterOuCriarAsync(cancellationToken);

        config.Provedor = request.Provedor.Trim();
        config.Modelo = request.Modelo.Trim();
        config.ProvedorEmbeddings = request.ProvedorEmbeddings.Trim();
        config.ModeloEmbeddings = request.ModeloEmbeddings.Trim();

        // Token nulo/vazio = mantém o atual; preenchido = cifra e substitui.
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            config.TokenCifrado = _protetor.Proteger(request.Token);
        }

        if (!string.IsNullOrWhiteSpace(request.TokenEmbeddings))
        {
            config.TokenEmbeddingsCifrado = _protetor.Proteger(request.TokenEmbeddings);
        }

        config.AtualizadoEm = DateTime.UtcNow;
        config.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IaConfiguracao> ObterOuCriarAsync(CancellationToken cancellationToken)
    {
        var config = await _db.IaConfiguracoes.FirstOrDefaultAsync(cancellationToken);
        if (config is not null)
        {
            return config;
        }

        config = new IaConfiguracao
        {
            Id = Guid.CreateVersion7(),
            Provedor = "anthropic",
            Modelo = "claude-opus-4-8",
            ProvedorEmbeddings = "voyage",
            ModeloEmbeddings = "voyage-3",
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.IaConfiguracoes.Add(config);
        await _db.SaveChangesAsync(cancellationToken);
        return config;
    }

    private static ConfiguracaoDto ParaDto(IaConfiguracao c) => new(
        c.Provedor,
        c.Modelo,
        c.ProvedorEmbeddings,
        c.ModeloEmbeddings,
        !string.IsNullOrEmpty(c.TokenCifrado),
        !string.IsNullOrEmpty(c.TokenEmbeddingsCifrado));
}
