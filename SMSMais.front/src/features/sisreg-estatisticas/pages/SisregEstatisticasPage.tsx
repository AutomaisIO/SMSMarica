import { useState } from 'react';
import { BarChart3 } from 'lucide-react';
import { usePermissao } from '@/shared/auth/authStore';
import { Input } from '@/shared/ui/Input';
import { Tabs, type Aba } from '@/shared/ui/Tabs';
import { ConfiguracaoOperadoresAba } from '../components/ConfiguracaoOperadoresAba';
import { EquipeAba } from '../components/EquipeAba';
import { IndividualAba } from '../components/IndividualAba';
import { PRESETS } from '../lib/formato';
import type { Periodo } from '../types';

/**
 * SISREG → Estatísticas: o trabalho dos operadores da regulação, da equipe e de cada pessoa.
 *
 * <p>Vem do export da agenda do SISREG já importado: cada agendamento traz o <b>Op. autorizador</b> e a
 * data da autorização. Só métricas seguras (15/09/2026): volume, valor, ritmo, tempos em dias,
 * amplitude e rankings. Comparecimento ficou de fora porque quem confirma é a unidade executante.</p>
 */
export function SisregEstatisticasPage() {
  const podeConfigurar = usePermissao('SisregConfiguracao', 'Consulta');
  const podeEditar = usePermissao('SisregConfiguracao', 'Edicao');

  const [presetAtivo, setPresetAtivo] = useState('30');
  const [periodo, setPeriodo] = useState<Periodo>(() => PRESETS.find((p) => p.id === '30')!.periodo());
  const [aba, setAba] = useState('equipe');
  const [operador, setOperador] = useState<string | null>(null);

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
      conteudo: <IndividualAba periodo={periodo} chave={operador} aoEscolher={setOperador} />,
    },
  ];
  if (podeConfigurar) {
    abas.push({ id: 'configuracao', rotulo: 'Configuração', conteudo: <ConfiguracaoOperadoresAba podeEditar={podeEditar} /> });
  }

  return (
    <div className="space-y-5">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <BarChart3 className="h-6 w-6 text-primary-600" />
          Estatísticas da regulação
        </h1>
        <p className="mt-1 max-w-4xl text-sm text-gray-600">
          O trabalho dos operadores que autorizam no SISREG — da equipe e de cada pessoa. Conta cada
          agendamento importado pelo seu <strong>Op. autorizador</strong> e pela data da autorização. A
          autorização não tem hora no SISREG, então todos os tempos são em dias.
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
