using System.Security.Cryptography;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes.Fhir;
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
    IPacienteFhirClient fhir,
    ILogger<TelefoneValidacaoService> logger) : ITelefoneValidacaoService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private const int MaxTentativas = 5;

    /// <summary>
    /// Forma canônica 55 + DDD + número (Maricá = DDD 21). Anti-burro: aceita com/sem DDI,
    /// com 0 de tronco (021), com/sem DDD e com qualquer pontuação.
    /// </summary>
    public static string Canonizar(string? telefone)
    {
        var d = Digitos(telefone);
        if (d.Length == 0) return d;
        // Já veio com DDI 55 (12 díg = fixo 8; 13 díg = celular 9).
        if (d.StartsWith("55", StringComparison.Ordinal) && (d.Length == 12 || d.Length == 13)) return d;
        d = d.TrimStart('0');                                   // remove 0 de tronco (021, 0xx)
        if (d.Length is 10 or 11) return "55" + d;              // DDD + número → falta só o DDI
        if (d.Length is 8 or 9) return "5521" + d;              // sem DDD → assume Maricá (21)
        return d.StartsWith("55", StringComparison.Ordinal) ? d : "5521" + d;
    }

    public async Task<TelefoneOtpEmitidoDto> EnviarCodigoAsync(string cpf, string numero, CancellationToken ct = default)
    {
        var cpfDig = CpfDigitos(cpf);
        var canon = Canonizar(numero);
        // 55 (DDI) + DDD (2) + número (>=8) = 12 dígitos no mínimo.
        if (canon.Length < 12)
            throw new ValidacaoException("telefone.invalido", "Informe um número de celular com DDD.");

        // Pré-checagem: o número já é contato principal de OUTRA pessoa? (UX melhor que falhar no confirmar)
        await GarantirNumeroLivreAsync(cpfDig, canon, ct);

        var codigo = GerarCodigo();
        cache.Set(Chave(cpfDig, canon), new Entry(codigo), Validade);
        var validadeSeg = (int)Validade.TotalSeconds;

        // Modo de teste (dev): não envia, devolve o código para a tela.
        if (config.GetValue("Tfd:Otp:ModoTeste", defaultValue: false))
        {
            logger.LogInformation("OTP de contato {Num} (CPF {Cpf}): {Cod} — modo teste.", canon, cpfDig, codigo);
            return new TelefoneOtpEmitidoDto("tela-teste", Mascarar(canon), validadeSeg);
        }

        var template = config.GetValue("Tfd:Otp:WhatsAppTemplate", "authzap")!;
        var idioma = config.GetValue("Tfd:Otp:WhatsAppIdioma", "pt_BR")!;
        var envio = await whatsapp.EnviarTemplateAutenticacaoAsync(canon, template, idioma, codigo, ct: ct);

        // Sem credenciais → o cliente "simula"; cai no fallback de tela (não trava).
        var simulado = envio.Ok && (envio.WaMessageId?.StartsWith("simulado-", StringComparison.Ordinal) ?? false);
        if (simulado)
        {
            logger.LogWarning("WhatsApp não configurado (envio simulado) ao validar contato {Num}.", canon);
            return new TelefoneOtpEmitidoDto("tela-teste", Mascarar(canon), validadeSeg);
        }

        if (!envio.Ok)
        {
            logger.LogWarning("Falha ao enviar OTP de contato {Num}: {Erro}", canon, envio.Erro);
            throw new ValidacaoException(
                "otp.envio_falhou", "Não conseguimos enviar o código agora. Tente novamente em instantes.");
        }

        return new TelefoneOtpEmitidoDto("whatsapp", Mascarar(canon), validadeSeg);
    }

    public async Task<TelefoneValidadoDto> ConfirmarCodigoAsync(
        string cpf, string numero, string codigo, CancellationToken ct = default, string origem = "painel")
    {
        var cpfDig = CpfDigitos(cpf);
        var canon = Canonizar(numero);
        var chave = Chave(cpfDig, canon);
        if (!cache.TryGetValue(chave, out Entry? entry) || entry is null)
            throw new ValidacaoException("otp.expirado", "Código expirado ou inexistente. Envie um novo código.");

        if (entry.Codigo != Digitos(codigo))
        {
            entry.Tentativas++;
            if (entry.Tentativas >= MaxTentativas) cache.Remove(chave);
            throw new ValidacaoException("otp.invalido", "Código inválido. Confira e tente de novo.");
        }

        cache.Remove(chave);
        var validadoEm = await MarcarValidadoInternoAsync(cpfDig, canon, origem, atual.UsuarioId, ct);
        return new TelefoneValidadoDto(canon, true, validadoEm);
    }

    public async Task MarcarValidadoAsync(
        string cpf, string numero, string origem, Guid? validadoPor, CancellationToken ct = default)
    {
        var cpfDig = CpfDigitos(cpf, lancar: false);
        var canon = Canonizar(numero);
        if (cpfDig.Length != 11 || canon.Length < 12) return;
        await MarcarValidadoInternoAsync(cpfDig, canon, origem, validadoPor, ct);
    }

    public async Task<TelefoneValidadoDto> ConsultarAsync(string cpf, string numero, CancellationToken ct = default)
    {
        var cpfDig = CpfDigitos(cpf, lancar: false);
        var canon = Canonizar(numero);
        if (cpfDig.Length != 11 || canon.Length < 12)
            return new TelefoneValidadoDto(canon, false, null);

        // Validado só quando o CONTATO daquele CPF é exatamente este número.
        var reg = await db.ContatosValidados.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Cpf == cpfDig && c.Numero == canon, ct);
        return new TelefoneValidadoDto(canon, reg is not null, reg?.ValidadoEm);
    }

    /// <summary>Lança 409 se o número já é contato principal de OUTRO CPF.</summary>
    private async Task GarantirNumeroLivreAsync(string cpfDig, string canon, CancellationToken ct)
    {
        var donoOutro = await db.ContatosValidados.AsNoTracking()
            .AnyAsync(c => c.Numero == canon && c.Cpf != cpfDig, ct);
        if (donoOutro)
            throw new ConflitoException(
                "telefone.duplicado",
                "Este número já é o contato principal de outra pessoa. Use um número diferente.");
    }

    private async Task<DateTime> MarcarValidadoInternoAsync(
        string cpfDig, string canon, string origem, Guid? por, CancellationToken ct)
    {
        await GarantirNumeroLivreAsync(cpfDig, canon, ct);

        var agora = DateTime.UtcNow;
        // Upsert por PESSOA (CPF): 1 contato principal por CPF; trocar o número libera o antigo.
        var existente = await db.ContatosValidados.FirstOrDefaultAsync(c => c.Cpf == cpfDig, ct);
        if (existente is null)
        {
            db.ContatosValidados.Add(new ContatoValidado
            {
                Id = Guid.CreateVersion7(),
                Cpf = cpfDig,
                Numero = canon,
                ValidadoEm = agora,
                Origem = origem,
                ValidadoPor = por,
            });
        }
        else
        {
            existente.Numero = canon;
            existente.ValidadoEm = agora;
            existente.Origem = origem;
            existente.ValidadoPor = por;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Contato {Num} validado para CPF {Cpf} (origem {Origem}).", canon, cpfDig, origem);

        // Projeta o "confirmado" no FHIR (marcador no telecom do Patient) para que a automação
        // — merge de edição, import, backfill — NUNCA toque neste número (ADR-0020, decisão #2).
        // Best-effort: se falhar, o contato_validado já está salvo e um backfill re-carimba.
        await EstamparConfirmadoNoFhirAsync(cpfDig, canon, agora, ct);
        return agora;
    }

    private async Task EstamparConfirmadoNoFhirAsync(string cpfDig, string canon, DateTime em, CancellationToken ct)
    {
        try
        {
            var bundle = await fhir.BuscarAsync(identifier: cpfDig, ct: ct);
            var patient = bundle.Entry.Select(e => e.Resource).OfType<Patient>().FirstOrDefault();
            if (patient?.Id is null) return;
            PatientMergeFhir.MarcarTelefoneConfirmado(patient, canon, new DateTimeOffset(em, TimeSpan.Zero));
            await fhir.AtualizarAsync(Guid.Parse(patient.Id), patient, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Falha ao estampar telefone confirmado no FHIR (CPF {Cpf}); contato_validado salvo, backfill re-carimba.",
                cpfDig);
        }
    }

    private static string Chave(string cpfDig, string canon) => $"otp:telefone:{cpfDig}:{canon}";
    private static string GerarCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
    private static string? Mascarar(string canon) => canon.Length < 4 ? null : "***-" + canon[^4..];

    private static string CpfDigitos(string? cpf, bool lancar = true)
    {
        var d = Digitos(cpf);
        if (d.Length != 11 && lancar)
            throw new ValidacaoException("cpf.invalido", "CPF da pessoa é obrigatório para validar o contato.");
        return d;
    }

    private sealed class Entry(string codigo)
    {
        public string Codigo { get; } = codigo;
        public int Tentativas { get; set; }
    }
}
