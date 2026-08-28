using System.Collections.Concurrent;
using SMSMais.Core.Integracoes.SisregWeb;

namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>
/// Cadastros já resolvidos nesta execução, por CNS.
///
/// <para><b>Para que serve:</b> a importação decide o paciente uma linha por vez, e cada decisão
/// que precisa do CADSUS custa uma ida à rede. Resolvendo antes, em paralelo, a importação
/// encontra a resposta pronta e continua serial — que é como ela precisa continuar, porque cria
/// paciente, solicitação e exame no mesmo <c>DbContext</c>.</para>
///
/// <para><b>Escopo curto de propósito.</b> É <c>Scoped</c>: vive o tempo de uma varredura e morre
/// com ela. Cadastro de cidadão muda (telefone, endereço, até o nome), e um cache que sobrevivesse
/// entre execuções passaria a servir dado velho sem ninguém perceber — o oposto do que a régua de
/// identidade do hub exige.</para>
/// </summary>
public sealed class CacheCadastroSer
{
    private readonly ConcurrentDictionary<string, ConsultaCnsRespostaDto> _porCns =
        new(StringComparer.Ordinal);

    /// <summary>CNS que a fonte respondeu "não existe" — evita repetir a pergunta cara.</summary>
    private readonly ConcurrentDictionary<string, byte> _semCadastro = new(StringComparer.Ordinal);

    public int Resolvidos => _porCns.Count;
    public int NaoEncontrados => _semCadastro.Count;

    public void Guardar(string cns, ConsultaCnsRespostaDto resposta) => _porCns[SoDigitos(cns)] = resposta;

    public void GuardarNaoEncontrado(string cns) => _semCadastro[SoDigitos(cns)] = 0;

    /// <summary>
    /// <c>true</c> = há resposta pronta (que pode ser <c>null</c>, significando "a fonte disse que
    /// não existe"). <c>false</c> = ninguém perguntou ainda, e quem chama vai à fonte.
    /// </summary>
    public bool TentarObter(string cns, out ConsultaCnsRespostaDto? resposta)
    {
        var chave = SoDigitos(cns);
        if (_porCns.TryGetValue(chave, out var achado))
        {
            resposta = achado;
            return true;
        }

        resposta = null;
        return _semCadastro.ContainsKey(chave);
    }

    private static string SoDigitos(string? v) =>
        v is null ? string.Empty : new string([.. v.Where(char.IsDigit)]);
}
