using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities.Integracoes;

namespace SMSMais.Core.Integracoes.ElevenLabs;

public sealed class ElevenLabsConfigService(SmsMaisDbContext db, IProtetorSegredos protetor) : IElevenLabsConfigService
{
    // A migration criou modelo_tts com default "" no banco; a linha pré-existente fica vazia.
    // Lendo, trata vazio como o padrão — não quebra TTS nem exige tocar na linha à mão.
    private const string ModeloTtsPadrao = "eleven_multilingual_v2";
    private static string TtsOuPadrao(string? m) => string.IsNullOrWhiteSpace(m) ? ModeloTtsPadrao : m;

    public async Task<ElevenLabsConfigDto> ObterAsync(CancellationToken ct = default)
    {
        var c = await ObterOuCriarAsync(ct);
        return new ElevenLabsConfigDto(c.BaseUrl, c.Modelo, c.VozId, TtsOuPadrao(c.ModeloTts), !string.IsNullOrEmpty(c.ApiKeyCifrada), c.Ativo);
    }

    public async Task AtualizarAsync(AtualizarElevenLabsConfigRequest request, CancellationToken ct = default)
    {
        var c = await ObterOuCriarAsync(ct);
        c.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? c.BaseUrl : request.BaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.Modelo)) c.Modelo = request.Modelo.Trim();
        if (!string.IsNullOrWhiteSpace(request.ModeloTts)) c.ModeloTts = request.ModeloTts.Trim();
        // Voz é opcional: string vazia LIMPA (volta a usar a primeira da conta); null mantém.
        if (request.VozId is not null) c.VozId = string.IsNullOrWhiteSpace(request.VozId) ? null : request.VozId.Trim();
        c.Ativo = request.Ativo;
        // Chave em branco mantém o que está gravado; a tela nunca reexibe o valor.
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) c.ApiKeyCifrada = protetor.Proteger(request.ApiKey.Trim());
        c.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ElevenLabsContexto> ObterContextoAsync(CancellationToken ct = default)
    {
        var c = await db.ElevenLabsConfiguracao.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new ValidacaoException("elevenlabs.nao_configurado", "Integração ElevenLabs ainda não configurada.");
        if (!c.Ativo) throw new ValidacaoException("elevenlabs.inativo", "Integração ElevenLabs está desativada.");
        if (string.IsNullOrEmpty(c.ApiKeyCifrada)) throw new ValidacaoException("elevenlabs.sem_chave", "Chave da API ElevenLabs não configurada.");
        return new ElevenLabsContexto(c.BaseUrl, c.Modelo, protetor.Revelar(c.ApiKeyCifrada), c.VozId, TtsOuPadrao(c.ModeloTts));
    }

    private async Task<ElevenLabsConfiguracao> ObterOuCriarAsync(CancellationToken ct)
    {
        var c = await db.ElevenLabsConfiguracao.FirstOrDefaultAsync(ct);
        if (c is not null) return c;
        c = new ElevenLabsConfiguracao { Id = Guid.CreateVersion7(), CriadoEm = DateTime.UtcNow };
        db.ElevenLabsConfiguracao.Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }
}
