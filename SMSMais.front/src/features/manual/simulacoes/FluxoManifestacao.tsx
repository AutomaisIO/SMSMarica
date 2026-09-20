import { useState } from 'react';
import { CheckCircle2, ClipboardList, Inbox, MessageSquareReply, Search, Send, Stamp } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

type Etapa = {
  id: string;
  rotulo: string;
  status: string;
  icone: LucideIcon;
  dia: string;
  detalhe: string;
};

/**
 * Uma manifestação inventada — protocolo 2026-000123 (exemplo), de "Joana da Silva (exemplo)" —
 * percorrendo o caminho feliz. Os dias são contados a partir do registro para a pessoa ver os dois
 * relógios (30 dias para o cidadão, 20 para a área) sem depender de calendário.
 */
const ETAPAS: Etapa[] = [
  {
    id: 'registro',
    rotulo: 'Registrada',
    status: 'Registrada',
    icone: ClipboardList,
    dia: 'Dia 0',
    detalhe:
      'Joana da Silva (exemplo) liga reclamando que a farmácia da unidade não tinha o remédio dela. A atendente registra: tipo Reclamação, identificada, canal Telefone. Nasce o protocolo 2026-000123 e o código de acesso — mostrado uma vez só. O relógio do cidadão começa: 30 dias.',
  },
  {
    id: 'triagem',
    rotulo: 'Em triagem',
    status: 'Em triagem',
    icone: Search,
    dia: 'Dia 1',
    detalhe:
      'A ouvidoria confere o tipo, escolhe assunto (Farmácia › Falta de medicamento), a unidade e a prioridade Normal. Nada disso é obrigatório para registrar — mas é o que faz a manifestação chegar à área certa.',
  },
  {
    id: 'encaminhada',
    rotulo: 'Encaminhada à área',
    status: 'Encaminhada à área',
    icone: Send,
    dia: 'Dia 1',
    detalhe:
      'Vai ao ponto de resposta da unidade. A área ganha o próprio relógio: 20 dias (prioridade Normal). Joana recebe no WhatsApp só "encaminhada à área responsável" — sem nome de ninguém. A área vê o relato, mas não vê quem é Joana.',
  },
  {
    id: 'area',
    rotulo: 'Respondida pela área',
    status: 'Respondida pela área',
    icone: Inbox,
    dia: 'Dia 9',
    detalhe:
      'O membro do ponto responde: o medicamento estava em falta no almoxarifado central, chegou no dia 7 e a paciente pode retirar. Essa resposta é interna — Joana ainda não vê nada.',
  },
  {
    id: 'validacao',
    rotulo: 'Ouvidoria valida',
    status: 'Em validação',
    icone: Stamp,
    dia: 'Dia 10',
    detalhe:
      'A ouvidoria lê a resposta da área. Se estivesse vaga ("providências foram tomadas"), devolveria para reanálise com metade do prazo. Está completa: hora de escrever para a cidadã.',
  },
  {
    id: 'respondida',
    rotulo: 'Respondida ao cidadão',
    status: 'Respondida ao cidadão',
    icone: MessageSquareReply,
    dia: 'Dia 10',
    detalhe:
      'Resposta conclusiva: análise do fato e providência adotada, resolutividade "Resolvida", situação final "Procede". Registrada no dia 10 de 30 — no prazo. Joana recebe o aviso e pode recorrer uma vez, se discordar.',
  },
  {
    id: 'concluida',
    rotulo: 'Concluída',
    status: 'Concluída',
    icone: CheckCircle2,
    dia: 'Dia 40',
    detalhe:
      'Sem recurso em 30 dias, o sistema conclui sozinho (a ouvidoria também pode concluir antes, à mão). Nada foi apagado: a linha do tempo inteira fica guardada, inclusive o que o cidadão não viu.',
  },
];

export function FluxoManifestacao() {
  const [ativa, setAtiva] = useState<string>('encaminhada');
  const etapa = ETAPAS.find((e) => e.id === ativa) ?? ETAPAS[0];

  return (
    <div className="max-w-3xl space-y-3">
      <ol className="grid gap-2 sm:grid-cols-4 lg:grid-cols-7">
        {ETAPAS.map((e) => {
          const Icone = e.icone;
          const selecionada = e.id === ativa;
          return (
            <li key={e.id}>
              <button
                type="button"
                onClick={() => setAtiva(e.id)}
                aria-pressed={selecionada}
                className={cn(
                  'flex h-full w-full flex-col items-center gap-1.5 rounded-theme-md border px-2 py-3 text-center transition-colors',
                  selecionada
                    ? 'border-primary-400 bg-primary-50 text-primary-900 shadow-sm'
                    : 'border-gray-200 bg-white text-gray-700 hover:border-primary-200 hover:bg-primary-50/50',
                )}
              >
                <span
                  className={cn(
                    'flex h-9 w-9 items-center justify-center rounded-full',
                    selecionada ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-600',
                  )}
                >
                  <Icone className="h-4 w-4" />
                </span>
                <span className="text-xs font-semibold leading-tight">{e.rotulo}</span>
                <span className="text-[11px] leading-tight text-gray-500">{e.dia}</span>
              </button>
            </li>
          );
        })}
      </ol>

      <div key={etapa.id} className="anim-entrada rounded-theme-md border border-gray-200 bg-white px-4 py-3">
        <p className="flex flex-wrap items-center gap-2 text-sm font-semibold text-gray-900">
          <span className="font-mono text-xs text-gray-500">2026-000123 (exemplo)</span>
          <span className="rounded-full bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-800">{etapa.status}</span>
          <span className="text-xs text-gray-500">{etapa.dia}</span>
        </p>
        <p className="mt-1 text-sm leading-relaxed text-gray-600">{etapa.detalhe}</p>
      </div>
    </div>
  );
}
