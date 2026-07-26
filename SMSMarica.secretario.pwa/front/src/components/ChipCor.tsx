import clsx from 'clsx';
import type { AguardandoPorCor } from '@/types/painel';
import { TRIAGEM } from '@/lib/triagem';
import { formatarInteiro, minutosLegiveis } from '@/lib/formatos';

/**
 * Chip de fila por cor: ponto colorido + nome escrito + quantidade, com a jornada
 * partida nos dois marcos — quanto se levou para CLASSIFICAR (intervalo fechado) e há
 * quanto tempo se espera o MÉDICO desde então (relógio correndo).
 *
 * Separados porque cobram donos diferentes: o primeiro é da enfermagem da porta, o
 * segundo é do plantão médico. Somados viram um número que não cobra ninguém — era o
 * caso do "desde a chegada", que fica só no tooltip. Cor nunca aparece sem o nome.
 */
export function ChipCor({ item }: { item: AguardandoPorCor }) {
  const estilo = TRIAGEM[item.cor];
  const vazio = item.qtd === 0;
  const triagem = item.minMedioAteClassificacao;
  const medico = item.minMedioDesdeClassificacao;

  return (
    <span
      className={clsx(
        'inline-flex flex-col gap-1 rounded-2xl border border-linha bg-papel px-3 py-1.5',
        'text-[13px] leading-none',
        vazio && 'opacity-55',
      )}
      title={montarTitulo(item, estilo.nome)}
    >
      <span className="inline-flex items-baseline gap-2">
        <span
          className="h-2.5 w-2.5 shrink-0 self-center rounded-full"
          style={{ backgroundColor: estilo.cor }}
        />
        <span className="font-medium text-tinta">{estilo.nome}</span>
        <span className="tnum font-display text-[15px] font-bold text-tinta">
          {formatarInteiro(item.qtd)}
        </span>
      </span>

      {(triagem != null || medico != null) && (
        <span className="tnum pl-[18px] text-[11px] text-grafite">
          {triagem != null && <>triagem {minutosLegiveis(triagem)}</>}
          {triagem != null && medico != null && ' · '}
          {medico != null && <>aguarda médico {minutosLegiveis(medico)}</>}
        </span>
      )}

      {/*
        Ninguém da cor foi classificado ainda — acontece na UPA, onde o balde
        SEM_CLASSIFICACAO é gente sem NENHUMA linha de classificação (32 pessoas às
        16h de 25/07). Sem este ramo o chip ficaria só com a contagem, escondendo que
        essas pessoas esperam a TRIAGEM, não o médico.
      */}
      {triagem == null && medico == null && !vazio && item.minMedioEspera != null && (
        <span className="tnum pl-[18px] text-[11px] text-grafite">
          na fila há {minutosLegiveis(item.minMedioEspera)} · sem classificação
        </span>
      )}
    </span>
  );
}

function montarTitulo(item: AguardandoPorCor, nome: string): string {
  if (item.qtd === 0) {
    return `${nome}: fila vazia`;
  }

  const partes: string[] = [];
  if (item.minMedioAteClassificacao != null) {
    partes.push(`${minutosLegiveis(item.minMedioAteClassificacao)} da chegada até a classificação`);
  }
  if (item.minMedioDesdeClassificacao != null) {
    partes.push(
      `${minutosLegiveis(item.minMedioDesdeClassificacao)} esperando o médico desde a classificação`,
    );
  }
  if (item.minMedioEspera != null) {
    partes.push(`${minutosLegiveis(item.minMedioEspera)} no total desde a chegada`);
  }

  return partes.length > 0 ? `${nome}, em média: ${partes.join('; ')}` : nome;
}
