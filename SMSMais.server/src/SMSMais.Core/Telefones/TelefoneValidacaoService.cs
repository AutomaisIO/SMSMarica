using System.Security.Cryptography;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Telefones.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Telefones;

public sealed class TelefoneValidacaoService(
    SmsMaisDbContext db,
    IWhatsAppCliente whatsapp,
    IMemoryCache cache,
    IConfiguration config,
    IUsuarioAtualAccessor atual,
    IPacienteFhirClient fhir,
    IDispensaContatoService dispensas,
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
        await GarantirNumeroLivreCanonAsync(cpfDig, canon, ct);

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
        // Pedido por uma pessoa (recepção ou o próprio cidadão) e é justamente o que PROVA o número:
        // barrar aqui deixaria um contato negado sem caminho de conserto.
        var envio = await whatsapp.EnviarTemplateAutenticacaoAsync(canon, template, idioma, codigo, ct: ct,
            origem: OrigemEnvioWhatsApp.Humano);

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
        // Código enviado pela recepção/cidadão: quem confirma é tratado como o próprio paciente.
        var validadoEm = await MarcarValidadoInternoAsync(cpfDig, canon, origem, atual.UsuarioId, exigirFhir: true, ct);
        return new TelefoneValidadoDto(canon, true, validadoEm);
    }

    public async Task MarcarValidadoAsync(
        string cpf, string numero, string origem, Guid? validadoPor, CancellationToken ct = default,
        VinculoContatoVerificado vinculo = VinculoContatoVerificado.Proprio)
    {
        var cpfDig = CpfDigitos(cpf, lancar: false);
        var canon = Canonizar(numero);
        if (cpfDig.Length != 11 || canon.Length < 12) return;
        // Caminho silencioso (login do PWA): melhor esforço no FHIR — não pode travar o login.
        await MarcarValidadoInternoAsync(cpfDig, canon, origem, validadoPor, exigirFhir: false, ct, vinculo);
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
        await GarantirNumeroLivreCanonAsync(cpfDig, canon, ct);

        var patient = await ObterPatientPorCpfAsync(cpfDig, ct)
            ?? throw new ValidacaoException(
                "telefone.sem_paciente",
                "Não encontramos o cadastro de paciente desta pessoa — o telefone é alterado no cadastro do paciente.");

        // Número que estava no cadastro ANTES da troca — decide se a dispensa cai (abaixo).
        var anterior = Canonizar(Pacientes.PacienteFhirMapper.ParaDto(patient).TelefonePrincipal);

        // Edição manual do principal: o merge derruba o marcador de verificado quando o
        // número muda (o novo nasce não-verificado; verificar depois é opcional).
        var nacional = canon.Length > 11 ? canon[2..] : canon;
        PatientMergeFhir.AplicarContatos(patient, principal: nacional,
            celular: null, residencial: null, email: null, manual: true);
        var pacienteId = Guid.Parse(patient.Id!);
        await fhir.AtualizarAsync(pacienteId, patient, ct);

        // Número NOVO merece uma tentativa nova de verificar: a dispensa foi dada olhando o
        // número antigo ("não tem celular", "é o da filha") e não vale para este. Só cai quando
        // o número de fato mudou — reescrever o mesmo número não deve punir a recepção.
        if (anterior != canon)
            await dispensas.RevogarAsync(pacienteId, "Telefone principal alterado", ct);

        logger.LogInformation("Telefone principal do CPF {Cpf} alterado manualmente para {Num} (usuário {Usuario}).",
            cpfDig, canon, atual.UsuarioId);
        return new TelefoneValidadoDto(canon, PatientMergeFhir.TelefoneEstaConfirmado(patient, canon), null);
    }

    public Task GarantirNumeroLivreAsync(string cpf, string numero, CancellationToken ct = default) =>
        GarantirNumeroLivreCanonAsync(CpfDigitos(cpf), Canonizar(numero), ct);

    /// <summary>
    /// Um número pode atender VÁRIOS pacientes — é o celular da mãe que recebe pelos três filhos
    /// (decisão de 17/09/2026). O que não pode é DUAS pessoas dizerem que o número é o delas
    /// mesmas: aí uma das duas está tomando o contato da outra (foi o caso das duas Márcias).
    /// <para>Por isso a trava só vale quando os dois lados declaram <b>próprio</b>: quem declara
    /// mãe/pai/responsável ou parente passa, e o vínculo fica gravado no cadastro.</para>
    /// </summary>
    private async Task GarantirNumeroLivreCanonAsync(
        string cpfDig, string canon, CancellationToken ct,
        VinculoContatoVerificado vinculo = VinculoContatoVerificado.Proprio)
    {
        if (vinculo != VinculoContatoVerificado.Proprio) return;

        // O hub guarda a forma nacional (sem DDI) — busca pelos últimos 11 dígitos.
        var nacional = canon.Length > 11 ? canon[^11..] : canon;
        var bundle = await fhir.BuscarAsync(telecom: nacional, ct: ct);
        var donoOutro = bundle.Entry.Select(e => e.Resource).OfType<Patient>().Any(p =>
            PatientMergeFhir.TelefoneEstaConfirmado(p, canon)
            && PatientMergeFhir.VinculoContatoConfirmado(p) == VinculoContatoVerificado.Proprio
            && CpfDoPatient(p) is { Length: 11 } outroCpf && outroCpf != cpfDig);
        if (donoOutro)
            throw new ConflitoException(
                "telefone.duplicado",
                "Este número já é o contato principal de outra pessoa. Se você recebe pela pessoa "
                + "(mãe, pai ou responsável), registre o contato com esse vínculo.");
    }

    private async Task<DateTime> MarcarValidadoInternoAsync(
        string cpfDig, string canon, string origem, Guid? por, bool exigirFhir, CancellationToken ct,
        VinculoContatoVerificado vinculo = VinculoContatoVerificado.Proprio)
    {
        await GarantirNumeroLivreCanonAsync(cpfDig, canon, ct, vinculo);

        var agora = DateTime.UtcNow;

        // Fonte ÚNICA: marcador no telecom do Patient FHIR (a tabela contato_validado foi
        // aposentada). Quando exigido (OTP confirmado pelo operador/cidadão), falha ALTO se não
        // conseguir carimbar — validação sem carimbo seria invisível para todo o sistema.
        // Verificado é conceito de PACIENTE: sem Patient no hub não há o que validar.
        var patientId = await EstamparConfirmadoNoFhirAsync(cpfDig, canon, agora, ct, vinculo);
        if (patientId is null && exigirFhir)
            throw new ValidacaoException(
                "telefone.sem_paciente",
                "Não encontramos o cadastro de paciente desta pessoa — a verificação de contato é feita no cadastro do paciente.");

        if (patientId is { } id)
        {
            // O verificado é mais forte que a dispensa: quem validou o número não precisa mais
            // do consentimento de não validar. Deixar a dispensa de pé manteria a régua fraca
            // (envio "assumindo risco") para alguém que já provou o número.
            await dispensas.RevogarAsync(id, "Contato verificado por código", ct);
            await LiberarComunicacoesRetidasAsync(id, agora, ct);
        }

        logger.LogInformation("Contato {Num} validado para CPF {Cpf} (origem {Origem}; FHIR {Fhir}).",
            canon, cpfDig, origem, patientId is not null ? "estampado" : "sem-paciente");
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

    /// <summary>Carimba o marcador no telecom e devolve o id do Patient; null = não estampou.</summary>
    private async Task<Guid?> EstamparConfirmadoNoFhirAsync(
        string cpfDig, string canon, DateTime em, CancellationToken ct,
        VinculoContatoVerificado vinculo = VinculoContatoVerificado.Proprio)
    {
        try
        {
            var patient = await ObterPatientPorCpfAsync(cpfDig, ct);
            if (patient?.Id is null) return null;
            PatientMergeFhir.MarcarTelefoneConfirmado(patient, canon, new DateTimeOffset(em, TimeSpan.Zero), vinculo);
            var id = Guid.Parse(patient.Id);
            await fhir.AtualizarAsync(id, patient, ct);
            return id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao estampar telefone confirmado no FHIR (CPF {Cpf}).", cpfDig);
            return null;
        }
    }

    /// <summary>
    /// Solta as comunicações que estavam RETIDAS por falta de contato verificado (resultado de
    /// exame e laudo). Verificou o telefone → o worker manda na próxima passagem, sem ninguém
    /// precisar lembrar de reenviar.
    /// </summary>
    private async Task LiberarComunicacoesRetidasAsync(Guid patientId, DateTime agora, CancellationToken ct)
    {
        var soltas = await db.ComunicacoesPaciente
            .Where(c => c.PacienteId == patientId
                        && c.Status == StatusComunicacao.AguardandoTelefoneVerificado)
            .ExecuteUpdateAsync(set => set
                .SetProperty(c => c.Status, StatusComunicacao.Pendente)
                .SetProperty(c => c.MotivoFalha, (string?)null)
                .SetProperty(c => c.ProximaTentativaEm, agora), ct);

        if (soltas > 0)
            logger.LogInformation(
                "Contato verificado do paciente {Paciente}: {N} comunicação(ões) retidas liberadas.",
                patientId, soltas);
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
