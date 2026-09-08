using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Dtos;
using SMSMais.Core.Integracoes.SisregWeb;

namespace SMSMais.Core.Integracoes.Proxy.Motores;

/// <summary>
/// Motor de CPF via CADSUS (SISREG III, tela <c>cadweb50</c>) — fallback do Hub/Receita.
/// Valida CPF + data de nascimento contra o cadastro do SUS: a ficha traz a data real, e
/// divergência é negativa autoritativa (mesma semântica do motor Receita). Não usa token —
/// a credencial é a sessão SISREG (store de Integrações, relogin automático).
/// <c>SituacaoCadastral</c> fica null: CADSUS não é a Receita.
/// </summary>
public sealed class SisregCadsusMotorCpf(IConsultaCnsService cadsus) : IMotorCpf
{
    public string Motor => MotoresProxy.SisregCadsus;

    public async Task<HubCpfRespostaDto> ConsultarAsync(
        string cpf, DateOnly dataNascimento, MotorExecucao cfg, CancellationToken cancellationToken)
    {
        ConsultaCnsRespostaDto ficha;
        try
        {
            ficha = await cadsus.ConsultarPorCpfAsync(cpf, cancellationToken);
        }
        catch (NaoEncontradoException)
        {
            throw new MotorNaoEncontrouException(Motor, "CPF não encontrado no cadastro do SUS (CADSUS).");
        }
        catch (ConflitoException)
        {
            // A guarda recusou a ficha: o CADSUS devolveu o cadastro de OUTRO CPF. Não é falha de
            // fonte — retentar (o que o bloco abaixo provoca) gastaria o orçamento anti-robô para
            // receber a mesma ficha errada. "Não encontrou" é o veredito honesto e deixa o executor
            // cair no próximo motor, que é o desfecho certo.
            throw new MotorNaoEncontrouException(
                Motor, "CPF não foi validado: o CADSUS devolveu o cadastro de outro CPF.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sessão caída/SISREG fora do ar/HTML inesperado → transitório: o executor retenta
            // e, esgotado, cai pro próximo motor (ou avisa "serviço não respondeu").
            throw new MotorIndisponivelException(Motor, "consulta ao SISREG falhou", ex);
        }

        // Mesma semântica do motor Receita: CPF e data de nascimento precisam conferir JUNTOS.
        // Ficha sem data (raro) não invalida — o nome/sexo ainda valem; mantém a data digitada.
        if (ficha.DataNascimento is { } nascimento && nascimento != dataNascimento)
        {
            throw new MotorNaoEncontrouException(
                Motor, "CPF não foi validado: a data de nascimento não confere com o cadastro do SUS.");
        }

        return new HubCpfRespostaDto(
            Cpf: cpf,
            Nome: ficha.Nome,
            DataNascimento: (ficha.DataNascimento ?? dataNascimento).ToString("dd/MM/yyyy"),
            SituacaoCadastral: null,
            Sexo: ficha.Sexo);
    }
}
