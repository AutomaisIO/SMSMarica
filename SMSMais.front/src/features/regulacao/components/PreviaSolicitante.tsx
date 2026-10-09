import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Eye, Loader2 } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';

import { previaRegras } from '../api/regulacaoApi';
import type { SistemaRegulacao } from '../types';
import { VisaoRegras } from './wizard/VisaoRegras';

const DESTINOS: { valor: SistemaRegulacao; rotulo: string }[] = [
  { valor: 'Ser', rotulo: 'SER' },
  { valor: 'Sernit', rotulo: 'SERNIT' },
  { valor: 'EsusSg', rotulo: 'ESUS de São Gonçalo' },
  { valor: 'Sisreg', rotulo: 'SISREG' },
];

/**
 * "Ver como o solicitante vê" (09/10/2026): o passo "Regras" do assistente para este
 * procedimento, com um paciente hipotético e sem resposta nenhuma.
 *
 * <p>Existe para quem cura as regras conferir, sem abrir solicitação de verdade, que a pergunta e
 * a caixinha aparecem para quem pede — é isso que poupa o técnico regulador de conferir o manual de
 * novo. Usa a mesma peça do assistente ({@link VisaoRegras}), e não um desenho à parte: prévia que
 * pudesse divergir da tela não provaria nada.</p>
 *
 * <p>Idade e sexo são opcionais: sem eles, a regra que depende do dado fica "a conferir", como fica
 * para o paciente sem data de nascimento no cadastro.</p>
 */
export function PreviaSolicitante({ procedimentoId }: { procedimentoId: string }) {
  const [sistema, setSistema] = useState<SistemaRegulacao>('Ser');
  const [idade, setIdade] = useState('');
  const [sexo, setSexo] = useState<'' | 'M' | 'F'>('');

  const idadeAnos = idade.trim() === '' ? null : Number(idade);
  const idadeValida = idadeAnos === null || (Number.isInteger(idadeAnos) && idadeAnos >= 0 && idadeAnos <= 130);

  const previa = useQuery({
    // Sob ['regulacao', 'regras']: ativar, criar ou excluir regra na tela invalida a prévia junto.
    queryKey: ['regulacao', 'regras', 'previa', procedimentoId, sistema, idadeAnos, sexo],
    queryFn: () =>
      previaRegras({ procedimentoId, sistema, idadeAnos, sexo: sexo === '' ? null : sexo, cid: null }),
    enabled: idadeValida,
  });

  return (
    <section className="rounded-lg border-2 border-dashed border-sky-300 bg-sky-50/40 p-4">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-sky-900">
        <Eye className="size-4" /> Como o solicitante vê o passo "Regras"
      </h2>
      <p className="mt-1 text-xs text-sky-900/80">
        Prévia: nada aqui é gravado e nenhuma solicitação é criada. Só as regras <strong>ativas</strong>{' '}
        aparecem.
      </p>

      <div className="mt-3 flex flex-wrap items-end gap-3 text-sm">
        <label className="flex flex-col gap-1 text-slate-700">
          Destino
          <select
            className="rounded border border-slate-300 bg-white px-2 py-1"
            value={sistema}
            onChange={(e) => setSistema(e.target.value as SistemaRegulacao)}
          >
            {DESTINOS.map((d) => (
              <option key={d.valor} value={d.valor}>
                {d.rotulo}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-slate-700">
          Idade do paciente
          <input
            type="number"
            min={0}
            max={130}
            placeholder="não informada"
            className="w-36 rounded border border-slate-300 bg-white px-2 py-1"
            value={idade}
            onChange={(e) => setIdade(e.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1 text-slate-700">
          Sexo
          <select
            className="rounded border border-slate-300 bg-white px-2 py-1"
            value={sexo}
            onChange={(e) => setSexo(e.target.value as '' | 'M' | 'F')}
          >
            <option value="">não informado</option>
            <option value="F">Feminino</option>
            <option value="M">Masculino</option>
          </select>
        </label>
      </div>

      <div className="mt-4">
        {!idadeValida && <p className="text-sm text-red-700">Idade entre 0 e 130, em anos inteiros.</p>}
        {previa.isLoading && (
          <p className="flex items-center gap-2 text-sm text-slate-500">
            <Loader2 className="size-4 animate-spin" /> Montando a prévia…
          </p>
        )}
        {previa.isError && (
          <p className="rounded bg-red-50 p-3 text-sm text-red-800">{extrairMensagemDeErro(previa.error)}</p>
        )}
        {previa.data && <VisaoRegras avaliacao={previa.data} somenteLeitura />}
      </div>
    </section>
  );
}
