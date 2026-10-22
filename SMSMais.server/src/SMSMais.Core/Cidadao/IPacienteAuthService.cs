using SMSMais.Core.Cidadao.Dtos;

namespace SMSMais.Core.Cidadao;

/// <summary>
/// Entrada do paciente no app (PWA e nativo), ADR-0078: CPF + data de nascimento → escolher um dos
/// números já conhecidos (ficha, ou e-SUS quando o canal permite) → código pelo WhatsApp. O código
/// nunca vai para um número digitado.
/// </summary>
public interface IPacienteAuthService
{
    /// <summary>
    /// Passo 1: confere CPF + nascimento (contra a ficha; sem ficha ou sem data, na Receita) e devolve
    /// os números possíveis. Quem já tem WhatsApp verificado vê só esse. Erro de dados conta tentativa:
    /// 5 em 30 min bloqueiam o CPF por 30 min. <paramref name="canal"/> é o cabeçalho
    /// <c>X-App-Canal</c> (decide se o e-SUS pode ser consultado).
    /// </summary>
    Task<EntradaCidadaoDto> IniciarEntradaAsync(IniciarEntradaRequest request, string? canal, CancellationToken ct = default);

    /// <summary>"Nenhum desses é meu": acrescenta os celulares do e-SUS que ainda não estavam na lista.</summary>
    Task<EntradaCidadaoDto> BuscarNoEsusAsync(string desafioId, CancellationToken ct = default);

    /// <summary>
    /// Envia o código para o número escolhido. Sem cadastro, o paciente só é criado quando o código é
    /// confirmado em <see cref="ValidarOtpAsync"/>.
    /// </summary>
    Task<OtpEmitidoDto> EnviarCodigoEntradaAsync(
        string desafioId, EnviarCodigoEntradaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Valida o OTP e abre a sessão (single-device). <paramref name="dispositivo"/> e
    /// <paramref name="ip"/> vêm do request HTTP (User-Agent / IP) só para rótulo/auditoria.
    /// </summary>
    Task<RespostaLoginPacienteDto> ValidarOtpAsync(
        ValidarOtpRequest request, string? dispositivo, string? ip, CancellationToken ct = default);
}
