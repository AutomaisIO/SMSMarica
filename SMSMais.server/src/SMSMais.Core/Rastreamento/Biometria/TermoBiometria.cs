using System.Security.Cryptography;
using System.Text;

namespace SMSMais.Core.Rastreamento.Biometria;

/// <summary>
/// Termo de ciência que o motorista aceita no primeiro acesso (ADR-0073 §8). Mudou o texto, muda a
/// <see cref="Versao"/>: o tablet mostra o que o servidor manda e devolve a versão aceita, e o
/// cadastro guarda versão + hash do texto. Sem nada institucional (nome do município etc.): o mesmo
/// texto serve a toda instância.
/// </summary>
public static class TermoBiometria
{
    public const string Versao = "2026-10-15";

    public const string Texto =
        "Termo de ciência — reconhecimento do rosto no tablet do veículo\n\n" +
        "Para entrar no tablet do veículo e registrar quem dirigiu cada translado, a Secretaria de " +
        "Saúde usa o reconhecimento do seu rosto.\n\n" +
        "O que é guardado: uma foto do seu rosto, tirada agora, e uma foto pequena a cada vez que " +
        "você entra no tablet. O tablet guarda só um código numérico calculado a partir da foto, " +
        "cifrado.\n\n" +
        "Para que serve: só para identificar você no tablet do veículo e registrar quem dirigiu. " +
        "Não é usado para outra finalidade.\n\n" +
        "Quem vê: a equipe da central de transporte com permissão no sistema.\n\n" +
        "Por quanto tempo: a foto do cadastro fica enquanto você for motorista cadastrado; as fotos " +
        "de cada entrada são apagadas em 90 dias.\n\n" +
        "Seus direitos: você pode pedir à central para apagar o cadastro do seu rosto a qualquer " +
        "momento. Nesse caso, a central libera a sua entrada no tablet por código.\n\n" +
        "Ao tocar em \"Concordo\", você declara que leu e entendeu este termo.";

    /// <summary>SHA-256 (hex) do texto, gravado junto com a versão.</summary>
    public static string Hash { get; } = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Texto)));
}
