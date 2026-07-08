using System.Security.Cryptography;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.Telefones.Dtos;

namespace SMSMarica.Core.Telefones;

public sealed class TelefoneValidacaoService(
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
        var validadoEm = await MarcarValidadoInternoAsync(cpfDig, canon, origem, atual.UsuarioId, exigirFhir: true, ct);
        return new TelefoneValidadoDto(canon, true, validadoEm);
    }

    public async Task MarcarValidadoAsync(
        string cpf, string numero, string origem, Guid? validadoPor, CancellationToken ct = default)
    {
        var cpfDig = CpfDigitos(cpf, lancar: false);
        var canon = Canonizar(numero);
        if (cpfDig.Length != 11 || canon.Length < 12) return;
        // Caminho silencioso (login do PWA): melhor esforço no FHIR — não pode travar o login.
        await MarcarValidadoInternoAsync(cpfDig, canon, origem, validadoPor, exigirFhir: false, ct);
    }

    public async Task<TelefoneValidadoDto> DefinirPrincipalAsync(
        string cpf, string numero, CancellationToken ct = default)
    {
        var cpfDig = CpfDigitos(cpf);
        var canon = Canonizar(numero);
        // 55 (DDI) + DDD (2) + número (>=8) = 12 dígitos no mínimo (fixo entra: quem digita
        // decide; o zap simplesmente não usa fixo).
        if (canon.Length < 12)
            throw new ValidacaoException("telefone.invalido", "Informe um número de telefone com DDD.");

        // O número não pode ser o contato CONFIRMADO de outra pessoa (caso das Márcias).
        await GarantirNumeroLivreAsync(cpfDig, canon, ct);

        var patient = await ObterPatientPorCpfAsync(cpfDig, ct)
            ?? throw new ValidacaoException(
                "telefone.sem_paciente",
                "Não encontramos o cadastro de paciente desta pessoa — o telefone é alterado no cadastro do paciente.");

        // Edição manual do principal: o merge derruba o marcador de verificado quando o
        // número muda (o novo nasce não-verificado; verificar depois é opcional).
        var nacional = canon.Length > 11 ? canon[2..] : canon;
        PatientMergeFhir.AplicarContatos(patient, principal: nacional,
            celular: null, residencial: null, email: null, manual: true);
        await fhir.AtualizarAsync(Guid.Parse(patient.Id!), patient, ct);

        logger.LogInformation("Telefone principal do CPF {Cpf} alterado manualmente para {Num} (usuário {Usuario}).",
            cpfDig, canon, atual.UsuarioId);
        return new TelefoneValidadoDto(canon, PatientMergeFhir.TelefoneEstaConfirmado(patient, canon), null);
    }

    /// <summary>Lança 409 se o número já é contato CONFIRMADO de OUTRO CPF (busca por telecom no hub).</summary>
    private async Task GarantirNumeroLivreAsync(string cpfDig, string canon, CancellationToken ct)
    {
        // O hub guarda a forma nacional (sem DDI) — busca pelos últimos 11 dígitos.
        var nacional = canon.Length > 11 ? canon[^11..] : canon;
        var bundle = await fhir.BuscarAsync(telecom: nacional, ct: ct);
        var donoOutro = bundle.Entry.Select(e => e.Resource).OfType<Patient>().Any(p =>
            PatientMergeFhir.TelefoneEstaConfirmado(p, canon)
            && CpfDoPatient(p) is { Length: 11 } outroCpf && outroCpf != cpfDig);
        if (donoOutro)
            throw new ConflitoException(
                "telefone.duplicado",
                "Este número já é o contato principal de outra pessoa. Use um número diferente.");
    }

    private async Task<DateTime> MarcarValidadoInternoAsync(
        string cpfDig, string canon, string origem, Guid? por, bool exigirFhir, CancellationToken ct)
    {
        await GarantirNumeroLivreAsync(cpfDig, canon, ct);

        var agora = DateTime.UtcNow;

        // Fonte ÚNICA: marcador no telecom do Patient FHIR (a tabela contato_validado foi
        // aposentada). Quando exigido (OTP confirmado pelo operador/cidadão), falha ALTO se não
        // conseguir carimbar — validação sem carimbo seria invisível para todo o sistema.
        // Verificado é conceito de PACIENTE: sem Patient no hub não há o que validar.
        var estampado = await EstamparConfirmadoNoFhirAsync(cpfDig, canon, agora, ct);
        if (!estampado && exigirFhir)
            throw new ValidacaoException(
                "telefone.sem_paciente",
                "Não encontramos o cadastro de paciente desta pessoa — a verificação de contato é feita no cadastro do paciente.");

        logger.LogInformation("Contato {Num} validado para CPF {Cpf} (origem {Origem}; FHIR {Fhir}).",
            canon, cpfDig, origem, estampado ? "estampado" : "sem-paciente");
        return agora;
    }

    private async Task<Patient?> ObterPatientPorCpfAsync(string cpfDig, CancellationToken ct)
    {
        var bundle = await fhir.BuscarAsync(identifier: cpfDig, ct: ct);
        return bundle.Entry.Select(e => e.Resource).OfType<Patient>().FirstOrDefault(p => p.Id is not null);
    }

    private static string? CpfDoPatient(Patient p) =>
        p.Identifier?.FirstOrDefault(i => i.System == PatientMergeFhir.SystemCpf)?.Value is { } v
            ? new string([.. v.Where(char.IsDigit)])
            : null;

    private async Task<bool> EstamparConfirmadoNoFhirAsync(string cpfDig, string canon, DateTime em, CancellationToken ct)
    {
        try
        {
            var patient = await ObterPatientPorCpfAsync(cpfDig, ct);
            if (patient?.Id is null) return false;
            PatientMergeFhir.MarcarTelefoneConfirmado(patient, canon, new DateTimeOffset(em, TimeSpan.Zero));
            await fhir.AtualizarAsync(Guid.Parse(patient.Id), patient, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao estampar telefone confirmado no FHIR (CPF {Cpf}).", cpfDig);
            return false;
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
