using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>
/// Consulta o cadastro do cidadão no CADSUS — <b>sem dizer por qual porta</b>.
///
/// <para>SISREG e SER leem a mesma base nacional; o que os separa é o custo. A porta do SISREG
/// (<c>cadweb50</c>) gasta o orçamento anti-robô do operador e, estourado, trava a unidade por 24h.
/// Quem importa não deveria precisar saber disso — daí esta interface: o chamador pede o cadastro,
/// a configuração decide de onde vem (<see cref="FonteCadastroPaciente"/>).</para>
///
/// <para>O contrato de erro é o mesmo dos dois lados: <c>NaoEncontradoException</c> quando o
/// cidadão não está no CADSUS, e qualquer outra exceção quando a fonte falhou. Essa distinção é o
/// que permite ao chamador classificar a falha (linha sem cadastro × fonte indisponível) sem
/// conhecer a porta.</para>
/// </summary>
public interface ICadastroPacienteService
{
    Task<ConsultaCnsRespostaDto> ConsultarPorCnsAsync(string cns, CancellationToken cancellationToken = default);

    Task<ConsultaCnsRespostaDto> ConsultarPorCpfAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>Fonte configurada agora — para a mensagem de falha citar a porta certa.</summary>
    Task<FonteCadastroPaciente> FonteAtualAsync(CancellationToken cancellationToken = default);
}
