using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes;

namespace SMSMarica.Core.Cidadao;

public sealed class PacienteAuthService(
    IPacientesService pacientes,
    ICidadaoSessaoService sessoes,
    IMemoryCache cache,
    IConfiguration config,
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

        // TODO(FT6): enviar 'codigo' por WhatsApp (Meta Cloud API) ao telefone do paciente.
        // Enquanto o WhatsApp não está ativo, o código é exibido na tela (modo teste).
        logger.LogInformation("OTP do paciente (CPF {Cpf}): {Codigo} — modo teste (envio WhatsApp pendente).", cpf, codigo);

        var modoTeste = config.GetValue("Tfd:Otp:ModoTeste", defaultValue: true);
        return new OtpEmitidoDto(
            Enviado: true,
            Canal: modoTeste ? "tela-teste" : "whatsapp",
            CodigoTeste: modoTeste ? codigo : null,
            ValidadeSegundos: (int)Validade.TotalSeconds);
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

    private sealed class OtpEntry(string codigo, Guid pacienteId, string nome, string cpf)
    {
        public string Codigo { get; } = codigo;
        public Guid PacienteId { get; } = pacienteId;
        public string Nome { get; } = nome;
        public string Cpf { get; } = cpf;
        public int Tentativas { get; set; }
    }
}
