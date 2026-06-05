using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.Sisreg.Dtos;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.Sisreg.Configuracao;

/// <summary>
/// Configuração singleton da integração SISREG: cria a linha na primeira leitura,
/// cifra senha/token (write-only) e resolve o contexto com segredos revelados para
/// o cliente. Espelha o padrão de <c>IaConfiguracaoService</c> (ADR-0011/0012).
/// </summary>
public sealed class SisregConfiguracaoService(
    SmsMaricaDbContext db,
    IProtetorSegredos protetor,
    IUsuarioAtualAccessor usuarioAtual) : ISisregConfiguracaoService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IProtetorSegredos _protetor = protetor;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<SisregConfiguracaoDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var config = await ObterOuCriarAsync(cancellationToken);
        return ParaDto(config);
    }

    public async Task AtualizarAsync(AtualizarSisregConfiguracaoRequest request, CancellationToken cancellationToken = default)
    {
        var config = await ObterOuCriarAsync(cancellationToken);

        config.BaseUrl = request.BaseUrl.Trim();
        config.Escopo = request.Escopo;
        config.Uf = request.Uf.Trim();
        config.Municipio = request.Municipio.Trim();
        config.CentraisReguladoras = request.CentraisReguladoras.Trim();
        config.TipoAutenticacao = request.TipoAutenticacao;
        config.Login = string.IsNullOrWhiteSpace(request.Login) ? null : request.Login.Trim();
        config.Ativo = request.Ativo;

        // Senha/token vazios = mantém o atual; preenchidos = cifra e substitui.
        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            config.SenhaCifrada = _protetor.Proteger(request.Senha);
        }

        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            config.TokenCifrado = _protetor.Proteger(request.Token);
        }

        config.AtualizadoEm = DateTime.UtcNow;
        config.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SisregContexto> ObterContextoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _db.SisregConfiguracoes.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new ValidacaoException("sisreg.nao_configurado",
                "Integração SISREG ainda não configurada. Configure credenciais e centrais reguladoras.");

        if (!config.Ativo)
        {
            throw new ValidacaoException("sisreg.inativo", "Integração SISREG está desativada.");
        }

        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            throw new ValidacaoException("sisreg.base_url", "URL base do SISREG não configurada.");
        }

        var centrais = (config.CentraisReguladoras ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var senha = string.IsNullOrEmpty(config.SenhaCifrada) ? null : _protetor.Revelar(config.SenhaCifrada);
        var token = string.IsNullOrEmpty(config.TokenCifrado) ? null : _protetor.Revelar(config.TokenCifrado);

        return new SisregContexto(
            BaseUrl: config.BaseUrl.EndsWith('/') ? config.BaseUrl : config.BaseUrl + "/",
            Escopo: config.Escopo,
            Uf: config.Uf,
            Municipio: config.Municipio,
            CentraisReguladoras: centrais,
            TipoAutenticacao: config.TipoAutenticacao,
            Login: config.Login,
            Senha: senha,
            Token: token);
    }

    private async Task<SisregConfiguracao> ObterOuCriarAsync(CancellationToken cancellationToken)
    {
        var config = await _db.SisregConfiguracoes.FirstOrDefaultAsync(cancellationToken);
        if (config is not null)
        {
            return config;
        }

        config = new SisregConfiguracao
        {
            Id = Guid.CreateVersion7(),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.SisregConfiguracoes.Add(config);
        await _db.SaveChangesAsync(cancellationToken);
        return config;
    }

    private static SisregConfiguracaoDto ParaDto(SisregConfiguracao c) => new(
        c.BaseUrl,
        c.Escopo,
        c.Uf,
        c.Municipio,
        c.CentraisReguladoras,
        c.TipoAutenticacao,
        c.Login,
        !string.IsNullOrEmpty(c.SenhaCifrada),
        !string.IsNullOrEmpty(c.TokenCifrado),
        c.Ativo);
}
