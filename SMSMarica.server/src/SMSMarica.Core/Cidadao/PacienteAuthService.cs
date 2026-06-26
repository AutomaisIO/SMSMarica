using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Telefones;

namespace SMSMarica.Core.Cidadao;

public sealed class PacienteAuthService(
    IPacientesService pacientes,
    ICidadaoSessaoService sessoes,
    IWhatsAppCliente whatsapp,
    IMemoryCache cache,
    IConfiguration config,
    ITelefoneValidacaoService telefoneValidacao,
    ILogger<PacienteAuthService> logger) : IPacienteAuthService
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);
    private const int MaxTentativas = 5;

    public async Task<OtpEmitidoDto> SolicitarOtpAsync(SolicitarOtpRequest request, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (cpf.Length != 11) throw new ValidacaoException("cpf", "CPF deve ter 11 dígitos.");

        var paciente = await pacientes.ObterPorCpfAsync(cpf, ct);
        if (paciente is null || !paciente.Ativo)
        {
            throw new ValidacaoException(
                "paciente.nao_encontrado",
                "Não encontramos um cadastro ativo para este CPF. Procure a sua unidade de saúde.");
        }

        var codigo = GerarCodigo();
        cache.Set(Chave(cpf), new OtpEntry(codigo, paciente.Id, paciente.NomeCompleto, paciente.Cpf ?? cpf), Validade);

        var validadeSeg = (int)Validade.TotalSeconds;

        // Modo de teste explícito (dev): não envia, devolve o código para a tela.
        if (config.GetValue("Tfd:Otp:ModoTeste", defaultValue: false))
        {
            logger.LogInformation("OTP do paciente (CPF {Cpf}): {Codigo} — modo teste forçado.", cpf, codigo);
            return new OtpEmitidoDto(true, "tela-teste", codigo, validadeSeg);
        }

        // Telefone do paciente (celular preferencial) a partir do cadastro FHIR.
        var dados = await pacientes.ObterPorIdAsync(paciente.Id, ct);
        var fone = PrimeiroTelefone(dados.TelefoneCelular, dados.TelefonePrincipal, dados.TelefoneResidencial);
        if (fone is null)
        {
            throw new ValidacaoException(
                "paciente.sem_telefone",
                "Não há telefone cadastrado para enviar o código. Procure a sua unidade de saúde.");
        }

        var template = config.GetValue("Tfd:Otp:WhatsAppTemplate", "authzap")!;
        var idioma = config.GetValue("Tfd:Otp:WhatsAppIdioma", "pt_BR")!;
        var envio = await whatsapp.EnviarTemplateAutenticacaoAsync(
            fone, template, idioma, codigo, pacienteId: paciente.Id, ct: ct);

        // Sem credenciais salvas no servidor → o cliente "simula". Não trava o login:
        // cai no fallback de tela e registra aviso para configurar o WhatsApp.
        var simulado = envio.Ok && (envio.WaMessageId?.StartsWith("simulado-", StringComparison.Ordinal) ?? false);
        if (simulado)
        {
            logger.LogWarning(
                "WhatsApp não configurado (envio simulado): OTP exibido na tela como fallback. CPF {Cpf}.", cpf);
            return new OtpEmitidoDto(true, "tela-teste", codigo, validadeSeg, Mascarar(fone));
        }

        if (!envio.Ok)
        {
            logger.LogWarning("Falha ao enviar OTP por WhatsApp (CPF {Cpf}): {Erro}", cpf, envio.Erro);
            throw new ValidacaoException(
                "otp.envio_falhou",
                "Não conseguimos enviar seu código agora. Tente novamente em instantes.");
        }

        return new OtpEmitidoDto(true, "whatsapp", null, validadeSeg, Mascarar(fone));
    }

    public async Task<RespostaLoginPacienteDto> ValidarOtpAsync(
        ValidarOtpRequest request, string? dispositivo, string? ip, CancellationToken ct = default)
    {
        var cpf = Digitos(request.Cpf);
        if (!cache.TryGetValue(Chave(cpf), out OtpEntry? entry) || entry is null)
        {
            throw new ValidacaoException("otp.expirado", "Código expirado ou inexistente. Solicite um novo código.");
        }

        if (entry.Codigo != Digitos(request.Codigo))
        {
            entry.Tentativas++;
            if (entry.Tentativas >= MaxTentativas) cache.Remove(Chave(cpf));
            throw new ValidacaoException("otp.invalido", "Código inválido. Confira e tente de novo.");
        }

        cache.Remove(Chave(cpf));

        // O cidadão acabou de provar posse do número (recebeu o OTP no WhatsApp): marca
        // como contato validado DA PESSOA (CPF). Nunca quebra o login se falhar.
        try
        {
            var dados = await pacientes.ObterPorIdAsync(entry.PacienteId, ct);
            var fone = PrimeiroTelefone(dados.TelefoneCelular, dados.TelefonePrincipal, dados.TelefoneResidencial);
            if (fone is not null)
                await telefoneValidacao.MarcarValidadoAsync(entry.Cpf, fone, "pwa-cidadao", null, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível marcar o telefone como validado no login do cidadão.");
        }

        // Abre a sessão single-device (revoga a anterior) e emite o token.
        var (token, _) = await sessoes.AbrirSessaoAsync(
            entry.PacienteId, entry.Nome, entry.Cpf, "otp-whatsapp", dispositivo, ip, ct);

        return new RespostaLoginPacienteDto(
            token, new PacienteSessaoDto(entry.PacienteId, entry.Nome, entry.Cpf));
    }

    private static string Chave(string cpf) => $"otp:paciente:{cpf}";

    private static string GerarCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    /// <summary>Primeiro telefone com pelo menos 10 dígitos (DDD + número), na ordem informada.</summary>
    private static string? PrimeiroTelefone(params string?[] candidatos) =>
        candidatos.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && Digitos(f).Length >= 10);

    /// <summary>Dica do destino para o usuário conferir, ex.: <c>***-1234</c>.</summary>
    private static string? Mascarar(string telefone)
    {
        var d = Digitos(telefone);
        return d.Length < 4 ? null : "***-" + d[^4..];
    }

    private sealed class OtpEntry(string codigo, Guid pacienteId, string nome, string cpf)
    {
        public string Codigo { get; } = codigo;
        public Guid PacienteId { get; } = pacienteId;
        public string Nome { get; } = nome;
        public string Cpf { get; } = cpf;
        public int Tentativas { get; set; }
    }
}
