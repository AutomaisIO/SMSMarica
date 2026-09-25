namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// Campanha de atendimento (ADR-0062): um período em que tudo o que for agendado para uma
/// <see cref="UnidadeId">unidade executante</see> é atendido, na verdade, em OUTRO lugar — o caso
/// que a criou foi a "Carreta da Mulher", unidade móvel regulada no SISREG como Secretaria de
/// Saúde. Para o paciente, o nome do local e o endereço da campanha substituem os da unidade.
///
/// <para>Encaixe: agendamento cuja unidade executante é <see cref="UnidadeId"/> e cuja data cai
/// em [<see cref="InicioEm"/>, <see cref="FimEm"/>]. Duas campanhas ATIVAS da mesma unidade não se
/// sobrepõem (validado no serviço).</para>
///
/// <para><see cref="ExigirConferenciaCadastral"/> desligada = o aviso sai direto com os dados do
/// agendamento, sem o desafio de CPF e nascimento e sem exigir telefone verificado. Em troca, o
/// link só CONFIRMA presença — não abre o app — salvo quando o número é o verificado do
/// paciente.</para>
/// </summary>
public class Campanha
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Unidade executante do SISREG em que a campanha é agendada (ex.: Secretaria).</summary>
    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>Início e fim do período (UTC). O agendamento encaixa se a data cai no intervalo.</summary>
    public DateTime InicioEm { get; set; }
    public DateTime FimEm { get; set; }

    /// <summary>Nome do local mostrado ao paciente no lugar do nome da unidade.</summary>
    public string LocalNome { get; set; } = string.Empty;

    /// <summary>Endereço em uma linha, como vai na mensagem ("Rua X, nº 1 – Centro, Maricá/RJ").</summary>
    public string LocalEndereco { get; set; } = string.Empty;

    /// <summary>Liga o desafio de CPF + nascimento antes dos dados. Desligada = entrega direta.</summary>
    public bool ExigirConferenciaCadastral { get; set; }

    /// <summary>Agendamento importado da unidade no período entra na fila sozinho, sem depender
    /// dos gatilhos de unidade/procedimento. Desligado = só pelo botão "Enviar".</summary>
    public bool EnvioAutomatico { get; set; } = true;

    public bool Ativa { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
