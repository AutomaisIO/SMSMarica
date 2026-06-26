using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Telefones.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Telefones;

public sealed class TelefoneValidacaoService(
    SmsMaricaDbContext db,
    IWhatsAppCliente whatsapp,
    IMemoryCache cache,
    IConfiguration config,
    IUsuarioAtualAccessor atual,
    ILogger<TelefoneValidacaoService> logger) : ITelefoneValidacaoService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private const int MaxTentativas = 5;

    /// <summary>Forma canônica: só dígitos + DDI Brasil (espelha o cliente WhatsApp).</summary>
    public static string Canonizar(string? telefone)
    {
        var d = Digitos(telefone);
        if (d.Length <= 11 && !d.StartsWith("55", StringComparison.Ordinal)) d = "55" + d;
        return d;
    }

    public async Task<TelefoneOtpEmitidoDto> EnviarCodigoAsync(string numero, CancellationToken ct = default)
    {
        var canon = Canonizar(numero);
        // 55 (DDI) + DDD (2) + número (>=8) = 12 dígitos no mínimo.
        if (canon.Length < 12)
            throw new ValidacaoException("telefone.invalido", "Informe um número de celular com DDD.");

        var codigo = GerarCodigo();
        cache.Set(Chave(canon), new Entry(codigo), Validade);
        var validadeSeg = (int)Validade.TotalSeconds;

        // Modo de teste (dev): não envia, devolve o código para a tela.
        if (config.GetValue("Tfd:Otp:ModoTeste", defaultValue: false))
        {
            logger.LogInformation("OTP de validação de telefone {Num}: {Cod} — modo teste.", canon, codigo);
            return new TelefoneOtpEmitidoDto("tela-teste", Mascarar(canon), validadeSeg);
        }

        var template = config.GetValue("Tfd:Otp:WhatsAppTemplate", "authzap")!;
        var idioma = config.GetValue("Tfd:Otp:WhatsAppIdioma", "pt_BR")!;
        var envio = await whatsapp.EnviarTemplateAutenticacaoAsync(canon, template, idioma, codigo, ct: ct);

        // Sem credenciais → o cliente "simula"; cai no fallback de tela (não trava).
        var simulado = envio.Ok && (envio.WaMessageId?.StartsWith("simulado-", StringComparison.Ordinal) ?? false);
        if (simulado)
        {
            logger.LogWarning("WhatsApp não configurado (envio simulado) ao validar telefone {Num}.", canon);
            return new TelefoneOtpEmitidoDto("tela-teste", Mascarar(canon), validadeSeg);
        }

        if (!envio.Ok)
        {
            logger.LogWarning("Falha ao enviar OTP de telefone {Num}: {Erro}", canon, envio.Erro);
            throw new ValidacaoException(
                "otp.envio_falhou", "Não conseguimos enviar o código agora. Tente novamente em instantes.");
        }

        return new TelefoneOtpEmitidoDto("whatsapp", Mascarar(canon), validadeSeg);
    }

    public async Task<TelefoneValidadoDto> ConfirmarCodigoAsync(string numero, string codigo, CancellationToken ct = default)
    {
        var canon = Canonizar(numero);
        if (!cache.TryGetValue(Chave(canon), out Entry? entry) || entry is null)
            throw new ValidacaoException("otp.expirado", "Código expirado ou inexistente. Envie um novo código.");

        if (entry.Codigo != Digitos(codigo))
        {
            entry.Tentativas++;
            if (entry.Tentativas >= MaxTentativas) cache.Remove(Chave(canon));
            throw new ValidacaoException("otp.invalido", "Código inválido. Confira e tente de novo.");
        }

        cache.Remove(Chave(canon));
        var validadoEm = await MarcarValidadoInternoAsync(canon, "painel", atual.UsuarioId, ct);
        return new TelefoneValidadoDto(canon, true, validadoEm);
    }

    public async Task MarcarValidadoAsync(string numero, string origem, Guid? validadoPor, CancellationToken ct = default)
    {
        var canon = Canonizar(numero);
        if (canon.Length < 12) return;
        await MarcarValidadoInternoAsync(canon, origem, validadoPor, ct);
    }

    public async Task<IReadOnlyList<TelefoneValidadoDto>> ConsultarAsync(
        IReadOnlyList<string> numeros, CancellationToken ct = default)
    {
        var canon = (numeros ?? [])
            .Select(Canonizar)
            .Where(n => n.Length >= 12)
            .Distinct()
            .ToArray();
        if (canon.Length == 0) return [];

        var validados = await db.NumerosValidados.AsNoTracking()
            .Where(n => canon.Contains(n.Numero))
            .ToDictionaryAsync(n => n.Numero, n => n.ValidadoEm, ct);

        return canon
            .Select(n => new TelefoneValidadoDto(
                n, validados.ContainsKey(n), validados.TryGetValue(n, out var dt) ? dt : null))
            .ToList();
    }

    private async Task<DateTime> MarcarValidadoInternoAsync(string canon, string origem, Guid? por, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var existente = await db.NumerosValidados.FirstOrDefaultAsync(n => n.Numero == canon, ct);
        if (existente is null)
        {
            db.NumerosValidados.Add(new NumeroValidado
            {
                Id = Guid.CreateVersion7(),
                Numero = canon,
                ValidadoEm = agora,
                Origem = origem,
                ValidadoPor = por,
            });
        }
        else
        {
            existente.ValidadoEm = agora;
            existente.Origem = origem;
            existente.ValidadoPor = por;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Número {Num} validado (origem {Origem}).", canon, origem);
        return agora;
    }

    private static string Chave(string canon) => $"otp:telefone:{canon}";
    private static string GerarCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
    private static string? Mascarar(string canon) => canon.Length < 4 ? null : "***-" + canon[^4..];

    private sealed class Entry(string codigo)
    {
        public string Codigo { get; } = codigo;
        public int Tentativas { get; set; }
    }
}
