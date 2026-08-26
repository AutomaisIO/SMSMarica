using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento;

public sealed class RoboConfiguracaoService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IRoboConfiguracaoService
{
    private const string ModeloPadraoInicial = "claude-haiku-4-5-20251001";

    public async Task<RoboConfiguracaoDto> ObterAsync(CancellationToken ct = default)
    {
        var cfg = await db.RoboConfiguracoes.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg is null
            ? new RoboConfiguracaoDto(false, RoboConfiguracao.PersonaGlobalPadrao, ModeloPadraoInicial, "Assistente virtual", null, null, null, null)
            : new RoboConfiguracaoDto(cfg.Ativo, cfg.PersonaGlobal, cfg.ModeloPadrao, cfg.NomeExibicao, cfg.MensagemHandOff, cfg.MensagemForaHorario, cfg.HoraAtendimentoHumanoInicio, cfg.HoraAtendimentoHumanoFim);
    }

    public async Task SalvarAsync(SalvarRoboConfiguracaoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PersonaGlobal))
            throw new ValidacaoException("personaGlobal", "Informe a persona base do robô.");
        if (string.IsNullOrWhiteSpace(request.ModeloPadrao))
            throw new ValidacaoException("modeloPadrao", "Informe o modelo padrão.");
        if (string.IsNullOrWhiteSpace(request.NomeExibicao))
            throw new ValidacaoException("nomeExibicao", "Informe o nome de exibição do robô.");

        var me = usuarioAtual.UsuarioId;
        var agora = DateTime.UtcNow;
        var cfg = await db.RoboConfiguracoes.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new RoboConfiguracao { Id = RoboConfiguracao.IdSingleton, CriadoEm = agora, CriadoPor = me };
            db.RoboConfiguracoes.Add(cfg);
        }

        cfg.Ativo = request.Ativo;
        cfg.PersonaGlobal = request.PersonaGlobal.Trim();
        cfg.ModeloPadrao = request.ModeloPadrao.Trim();
        cfg.NomeExibicao = request.NomeExibicao.Trim();
        cfg.MensagemHandOff = Normalizar(request.MensagemHandOff);
        cfg.MensagemForaHorario = Normalizar(request.MensagemForaHorario);
        cfg.HoraAtendimentoHumanoInicio = request.HoraAtendimentoHumanoInicio;
        cfg.HoraAtendimentoHumanoFim = request.HoraAtendimentoHumanoFim;
        cfg.AtualizadoEm = agora;
        cfg.AtualizadoPor = me;

        await db.SaveChangesAsync(ct);
    }

    private static string? Normalizar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
