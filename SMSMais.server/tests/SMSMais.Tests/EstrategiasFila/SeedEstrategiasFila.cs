using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Tests.EstrategiasFila;

/// <summary>
/// Um procedimento inteiro para os testes de cenário: um GRUPO com escala regulada numa unidade,
/// um ITEM que só aparece nas marcações, gente na fila pelos dois nomes, marcações passadas.
///
/// <para>Tudo com sufixo aleatório — código, nomes, CPF, unidade — porque a bancada guarda as linhas
/// entre execuções e a família casa por código/nome: um "0229000" fixo colidiria com o de outros
/// testes e com o que outra rodada deixou para trás.</para>
/// </summary>
internal sealed class SeedEstrategiasFila
{
    public Guid UnidadeId { get; private init; }
    public string CodigoGrupo { get; private init; } = string.Empty;
    public string CodigoItem { get; private init; } = string.Empty;
    public string NomeGrupo { get; private init; } = string.Empty;
    public string NomeItem { get; private init; } = string.Empty;
    public string Cpf { get; private init; } = string.Empty;
    public DateOnly Hoje { get; private init; }

    /// <summary>Pessoas na fila hoje (grupo + item).</summary>
    public const int NaFila = 5;

    /// <summary>Marcações passadas (todas com data de pedido e data agendada dentro das séries).</summary>
    public const int Marcacoes = 4;

    /// <summary>Vagas de regulação por bloco (1ª vez + reserva); 2 blocos por semana.</summary>
    public const int VagasPorBloco = 10;

    public static async Task<SeedEstrategiasFila> CriarAsync(SmsMaisDbContext db)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var prefixo = "9" + Random.Shared.Next(100, 999);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        var seed = new SeedEstrategiasFila
        {
            UnidadeId = Guid.NewGuid(),
            CodigoGrupo = prefixo + "000",
            CodigoItem = prefixo + "010",
            NomeGrupo = $"GRUPO - TESTE ESTRATEGIA {sufixo}",
            NomeItem = $"TESTE ESTRATEGIA ITEM {sufixo}",
            Cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(),
            Hoje = hoje,
        };

        db.Unidades.Add(new Unidade
        {
            Id = seed.UnidadeId,
            Nome = $"UNIDADE ESTRATEGIA {sufixo}",
            Cnes = Random.Shared.Next(1_000_000, 9_999_999).ToString(),
            CriadoEm = DateTime.UtcNow,
        });

        // Escala regulada: segunda e quarta, 08–12, 5 de 1ª vez + 5 de reserva, vigente das últimas
        // 10 semanas até daqui a 8.
        foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Wednesday })
        {
            db.SisregEscalas.Add(new SisregEscala
            {
                Id = Guid.NewGuid(),
                CodigoEscala = Random.Shared.Next(100_000_000, 999_999_999).ToString(),
                UnidadeId = seed.UnidadeId,
                Cnes = "1234567",
                UnidadeNomeSisreg = $"UNIDADE ESTRATEGIA {sufixo}",
                ProfissionalCpf = seed.Cpf,
                ProfissionalNome = $"DR TESTE {sufixo}",
                CboCodigo = "225120",
                CboDescricao = "MEDICO CARDIOLOGISTA",
                ProcedimentoCodigo = seed.CodigoGrupo,
                ProcedimentoNome = seed.NomeGrupo,
                EhGrupo = true,
                DiaSemana = dia,
                HoraInicio = new TimeOnly(8, 0),
                HoraFim = new TimeOnly(12, 0),
                VigenciaInicio = hoje.AddDays(-70),
                VigenciaFim = hoje.AddDays(56),
                VagasPrimeiraVez = 5,
                VagasRetorno = 0,
                VagasReserva = 5,
                VagasTotal = 10,
                Status = StatusEscalaSisreg.Ativa,
                AgendaLocal = false,
                VistoEm = DateTime.UtcNow,
                CriadoEm = DateTime.UtcNow,
            });
        }

        // Fila: 3 pelo nome do grupo, 2 pelo nome do item, e 1 que saiu sem agendar.
        for (var i = 0; i < NaFila + 1; i++)
        {
            var saiu = i == NaFila;
            db.SisregFilaPendentes.Add(new SisregFilaPendente
            {
                Id = Guid.NewGuid(),
                CodigoSolicitacao = Codigo(),
                DataSolicitacao = hoje.AddDays(-10 - i * 7),
                Risco = i % 3,
                ProcedimentoNome = i < 3 ? seed.NomeGrupo : seed.NomeItem,
                PacienteNome = $"PACIENTE {i}",
                PrimeiroVistoEm = DateTime.UtcNow.AddDays(-10),
                UltimoVistoEm = saiu ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow,
                SaiuEm = saiu ? DateTime.UtcNow.AddDays(-1) : null,
                SaiuPara = saiu ? SaidaDaFilaSisreg.SaiuSemAgendar : null,
                CriadoEm = DateTime.UtcNow,
            });
        }

        // Marcações: pedidas há 20 dias, agendadas há 9 (dentro das 8 semanas de ocupação).
        for (var i = 0; i < Marcacoes; i++)
        {
            db.Solicitacoes.Add(new Solicitacao
            {
                Id = Guid.NewGuid(),
                CodigoSolicitacao = Codigo(),
                PacienteId = Guid.NewGuid(),
                Categoria = CategoriaSolicitacao.Consulta,
                UnidadeExecutanteId = seed.UnidadeId,
                ProfissionalExecutanteCpf = seed.Cpf,
                SolicitanteNome = "DR SOLICITANTE",
                ProcedimentoTexto = seed.NomeItem,
                ProcedimentoCodigoSisreg = seed.CodigoItem,
                Status = StatusSolicitacao.Agendada,
                StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
                Prioridade = PrioridadeSolicitacao.Eletiva,
                DataSolicitacao = hoje.AddDays(-20),
                DataAgendada = DateTime.SpecifyKind(hoje.AddDays(-9).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc),
                CriadoEm = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return seed;
    }

    private static string Codigo() => Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();
}
