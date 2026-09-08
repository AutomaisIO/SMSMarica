using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>
/// Escolhe a porta do CADSUS conforme <c>sisreg_configuracao.fonte_cadastro_paciente</c>.
///
/// <para><b>Lê a configuração a cada consulta, de propósito.</b> Cachear pareceria barato — é um
/// SELECT numa linha singleton —, mas o momento em que alguém troca a fonte é justamente o momento
/// em que o SISREG começou a exigir CAPTCHA e a importação está rodando. Uma configuração em cache
/// faria a troca só valer no próximo restart, com o operador olhando a tela já mudada e as
/// pendências continuando a se acumular.</para>
/// </summary>
public sealed class CadastroPacienteRoteador(
    SmsMaisDbContext db,
    IConsultaCnsService sisreg,
    ISerCadastroPacienteService ser,
    CacheCadastroSer cache,
    ILogger<CadastroPacienteRoteador> logger) : ICadastroPacienteService
{
    public async Task<ConsultaCnsRespostaDto> ConsultarPorCnsAsync(
        string cns, CancellationToken cancellationToken = default)
    {
        // A pré-carga já pode ter resolvido este CNS em paralelo, antes da importação começar.
        // "Tem resposta e é null" significa que a fonte disse que não existe — repetir a pergunta
        // custaria uma ida à rede para ouvir o mesmo não.
        if (cache.TentarObter(cns, out var pronto))
        {
            return pronto ?? throw new NaoEncontradoException(
                "cadastro.paciente_nao_encontrado",
                "O CADSUS não conhece este CNS (consultado na pré-carga desta execução).");
        }

        return await ConsultarAsync(
            cns, porCpf: false,
            f => f == FonteCadastroPaciente.Sisreg
                ? sisreg.ConsultarPorCnsAsync(cns, cancellationToken)
                : ser.ConsultarPorCnsAsync(cns, cancellationToken),
            cancellationToken);
    }

    public Task<ConsultaCnsRespostaDto> ConsultarPorCpfAsync(
        string cpf, CancellationToken cancellationToken = default) =>
        ConsultarAsync(
            cpf, porCpf: true,
            f => f == FonteCadastroPaciente.Sisreg
                ? sisreg.ConsultarPorCpfAsync(cpf, cancellationToken)
                : ser.ConsultarPorCpfAsync(cpf, cancellationToken),
            cancellationToken);

    public async Task<FonteCadastroPaciente> FonteAtualAsync(CancellationToken cancellationToken = default) =>
        await db.SisregConfiguracoes.AsNoTracking()
            .Select(c => (FonteCadastroPaciente?)c.FonteCadastroPaciente)
            .FirstOrDefaultAsync(cancellationToken)
        // Sem linha de configuração (instância nova), vale o caminho histórico.
        ?? FonteCadastroPaciente.Sisreg;

    private async Task<ConsultaCnsRespostaDto> ConsultarAsync(
        string chave, bool porCpf,
        Func<FonteCadastroPaciente, Task<ConsultaCnsRespostaDto>> consultar,
        CancellationToken cancellationToken)
    {
        var fonte = await FonteAtualAsync(cancellationToken);

        if (fonte != FonteCadastroPaciente.SerComFallbackSisreg)
            return Conferir(chave, porCpf, await consultar(fonte), fonte);

        try
        {
            return Conferir(chave, porCpf, await consultar(FonteCadastroPaciente.Ser),
                            FonteCadastroPaciente.Ser);
        }
        catch (NaoEncontradoException)
        {
            // O SER RESPONDEU: o cidadão não está no CADSUS. Perguntar ao SISREG a mesma coisa
            // gastaria o orçamento anti-robô para ouvir a mesma resposta. O fallback é para falha
            // da fonte, não para ausência do dado.
            throw;
        }
        catch (ConflitoException)
        {
            // A guarda de identidade recusou a ficha. Isso é uma RESPOSTA da fonte, não uma falha
            // dela: cair no SISREG perguntaria a mesma coisa à mesma base nacional, gastando o
            // orçamento anti-robô para, no melhor caso, ouvir o mesmo. O caso precisa de gente.
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Cadastro: o SER falhou ({Erro}) — consultando o SISREG (fallback configurado).",
                ex.Message);
            return Conferir(chave, porCpf, await consultar(FonteCadastroPaciente.Sisreg),
                            FonteCadastroPaciente.Sisreg);
        }
    }

    /// <summary>
    /// A ficha que voltou é de quem perguntamos? Ver <see cref="GuardaIdentidadeCadastro"/> para o
    /// porquê da régua ser diferente para CPF e para CNS.
    /// </summary>
    private ConsultaCnsRespostaDto Conferir(
        string chave, bool porCpf, ConsultaCnsRespostaDto resposta, FonteCadastroPaciente fonte)
    {
        var devolvido = porCpf ? resposta.Cpf : resposta.Cns;

        switch (GuardaIdentidadeCadastro.Conferir(chave, devolvido, porCpf))
        {
            case VeredictoCadastro.TrocaDeIdentidade:
                // Sem PII no log: o par de identificadores já é suficiente para achar o caso, e o
                // log da API não é lugar de nome de cidadão.
                logger.LogError(
                    "Cadastro: {Fonte} devolveu CPF diferente do perguntado — ficha RECUSADA. "
                    + "Pedido {Pedido}, devolvido {Devolvido}.",
                    fonte, chave, devolvido);

                throw new ConflitoException(
                    "cadastro.identidade_divergente",
                    "A consulta ao CADSUS devolveu o cadastro de outro CPF. O cadastro não foi "
                    + "preenchido automaticamente porque pode ser de outra pessoa — confira "
                    + "manualmente antes de continuar.");

            case VeredictoCadastro.OutroCns:
                // Não é erro: a pessoa tem mais de um CNS. Fica registrado porque é o sinal que
                // permite ao hub guardar os dois números (cns_todos) em vez de trocar um pelo outro.
                logger.LogInformation(
                    "Cadastro: {Fonte} devolveu outro CNS ({Devolvido}) para o CNS {Pedido} — "
                    + "mesma pessoa com mais de um número; os dois valem como identificador.",
                    fonte, devolvido, chave);
                break;
        }

        return resposta;
    }
}
