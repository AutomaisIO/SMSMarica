using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Lista unidades de saúde da rede (nome + endereço) para o robô poder dizer ONDE fica um posto,
/// em vez de inventar endereço. Aceita um termo de busca (nome ou bairro); sem termo, devolve uma
/// amostra.
///
/// O que este comando NÃO é: uma oferta de atendimento. O robô não marca consulta, não diz que a
/// unidade vai atender, não promete horário nem encaixe — a orientação é sempre procurar o posto
/// onde a pessoa JÁ é atendida. Por isso o resultado carrega essa instrução junto com os dados:
/// informação de endereço sem essa moldura vira promessa de atendimento na cabeça de quem lê.
/// </summary>
public sealed class ConsultarUnidadesComando(SmsMaisDbContext db) : IRoboComando
{
    private const int Maximo = 6;

    public ComandoRobo Comando => ComandoRobo.ConsultarUnidades;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:consultar_unidades";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        var termo = (GateIdentidade.LerString(ctx.Args, "termo") ?? string.Empty).Trim();

        var q = db.Unidades.AsNoTracking().Where(u => u.Ativo && !u.Externa);
        if (termo.Length >= 3)
        {
            var t = $"%{termo}%";
            q = q.Where(u => EF.Functions.ILike(u.Nome, t)
                || (u.Endereco != null && EF.Functions.ILike(u.Endereco.Bairro, t)));
        }

        var achadas = await q.OrderBy(u => u.Nome).Take(Maximo)
            .Select(u => new
            {
                u.Nome,
                u.Endereco!.Logradouro,
                u.Endereco.Numero,
                u.Endereco.Bairro,
            })
            .ToListAsync(ct);

        if (achadas.Count == 0)
            return new(false,
                "Não encontrei unidade com esse nome ou bairro. NÃO invente endereço: oriente a "
                + "pessoa a procurar o posto de saúde onde ela já é atendida.");

        var linhas = achadas.Select(u =>
        {
            var numero = string.IsNullOrWhiteSpace(u.Numero) ? string.Empty : $", {u.Numero}";
            var bairro = string.IsNullOrWhiteSpace(u.Bairro) ? string.Empty : $" — {u.Bairro}";
            return $"- {u.Nome}: {u.Logradouro}{numero}{bairro}";
        });

        return new(true,
            string.Join("\n", linhas)
            + "\n\nUse SÓ para informar onde fica. NÃO ofereça consulta, atendimento, horário nem "
            + "encaixe, e NÃO diga que a unidade vai atender: oriente sempre a procurar o posto de "
            + "saúde onde a pessoa já é atendida.");
    }
}
