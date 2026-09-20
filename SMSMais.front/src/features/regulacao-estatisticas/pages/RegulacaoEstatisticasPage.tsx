import { useEffect, useState } from 'react';
import { BarChart3 } from 'lucide-react';
import { usePermissao } from '@/shared/auth/authStore';
import { PRESETS } from '@/shared/lib/estatisticasPeriodo';
import { Input } from '@/shared/ui/Input';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { ConfiguracaoOperadoresAba } from '../components/ConfiguracaoOperadoresAba';
import { EquipeAba } from '../components/EquipeAba';
import { IndividualAba } from '../components/IndividualAba';
import { FONTES } from '../lib/rotulos';
import type { FonteExterna, Periodo } from '../types';

/**
 * SER / SERNIT → Estatísticas: o trabalho dos operadores na fila espelhada, da equipe e de cada pessoa.
 *
 * <p>Vem da trilha "Histórico da Solicitação" que a varredura captura: cada evento traz quem fez, quando
 * (com hora — ao contrário do SISREG) e o verbo (agendar, cancelar, pendenciar, FollowUP…). A tela é a
 * mesma para as duas fontes; o que muda é a rota da API e os textos. Cada fonte tem o seu módulo de
 * permissão (68–70), desligado por padrão fora do Admin.</p>
 */
export function RegulacaoEstatisticasPage({ fonte }: { fonte: FonteExterna }) {
  const podeConfigurar = usePermissao('RegulacaoConfiguracao', 'Consulta');
  const podeEditar = usePermissao('RegulacaoConfiguracao', 'Edicao');
  const info = FONTES[fonte];

  const [presetAtivo, setPresetAtivo] = useState('30');
  const [periodo, setPeriodo] = useState<Periodo>(() => PRESETS.find((p) => p.id === '30')!.periodo());
  const [aba, setAba] = useState('equipe');
  const [operador, setOperador] = useState<string | null>(null);

  // A mesma página serve SER e SERNIT: ao trocar de fonte pela navegação, o operador escolhido
  // não existe na outra fila.
  useEffect(() => {
    setOperador(null);
    setAba('equipe');
  }, [fonte]);

  function escolherPreset(id: string) {
    const p = PRESETS.find((x) => x.id === id);
    if (!p) return;
    setPresetAtivo(id);
    setPeriodo(p.periodo());
  }

  function mudarData(campo: keyof Periodo, valor: string) {
    if (!valor) return;
    setPresetAtivo('');
    setPeriodo((p) => ({ ...p, [campo]: valor }));
  }

  const abas: Aba[] = [
    {
      id: 'equipe',
      rotulo: 'Equipe',
      conteudo: (
        <EquipeAba
          fonte={fonte}
          periodo={periodo}
          aoAbrirOperador={(chave) => {
            setOperador(chave);
            setAba('individual');
          }}
        />
      ),
    },
    {
      id: 'individual',
      rotulo: 'Individual',
      conteudo: <IndividualAba fonte={fonte} periodo={periodo} chave={operador} aoEscolher={setOperador} />,
    },
  ];
  if (podeConfigurar) {
    abas.push({
      id: 'configuracao',
      rotulo: 'Configuração',
      conteudo: <ConfiguracaoOperadoresAba fonte={fonte} podeEditar={podeEditar} />,
    });
  }

  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <BarChart3 className="h-6 w-6 text-primary-600" />
          Estatísticas do {info.sigla}
        </h1>
        <p className="mt-1 max-w-4xl text-sm text-gray-600">
          O trabalho dos operadores no {info.nome} — da equipe e de cada pessoa. Conta cada evento da
          trilha "Histórico da Solicitação" pelo seu <strong>Usuário</strong> e pela data e hora em que
          foi registrado (relógio de Brasília). Agendamentos, cancelamentos, pendências e FollowUPs
          aparecem separados; o resto entra só no total de ações.
        </p>
      </header>

      {aba !== 'configuracao' ? (
        <div className="flex flex-wrap items-end gap-3 rounded-xl border border-gray-200 bg-white p-3 shadow-sm">
          <div className="flex flex-wrap gap-1">
            {PRESETS.map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => escolherPreset(p.id)}
                className={`rounded-full px-3 py-1 text-xs ${
                  presetAtivo === p.id ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                }`}
              >
                {p.rotulo}
              </button>
            ))}
          </div>
          <label className="text-xs text-gray-600">
            De
            <Input type="date" value={periodo.de} onChange={(e) => mudarData('de', e.target.value)} className="mt-1" />
          </label>
          <label className="text-xs text-gray-600">
            Até
            <Input type="date" value={periodo.ate} onChange={(e) => mudarData('ate', e.target.value)} className="mt-1" />
          </label>
          <p className="text-[11px] text-gray-400">Até 366 dias. Comparado sempre com o período anterior de mesmo tamanho.</p>
        </div>
      ) : null}

      <Tabs abas={abas} abaAtiva={aba} aoTrocarAba={setAba} />
    </div>
  );
}
