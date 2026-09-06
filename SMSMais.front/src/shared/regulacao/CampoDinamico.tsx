import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';

import {
  paraBr,
  paraIso,
  type CampoDinamicoRegulacao,
  type NomeSistemaRegulacao,
} from './tiposCampo';

type Props = {
  c: CampoDinamicoRegulacao;
  valor: string;
  desabilitado?: boolean;
  onChange: (v: string) => void;
  /** Só entra nas mensagens ao operador ("recopie o catálogo do SER…"). */
  sistema: NomeSistemaRegulacao;
};

/**
 * Desenha o campo conforme o tipo que o catálogo guardou para ele.
 *
 * <p>Compartilhado por SER, SERNIT e pelo fluxo Externo da Regulação (ADR-0052) — os três são a
 * mesma aplicação JSF em instalações diferentes, e antes desta extração existiam duas cópias
 * idênticas deste arquivo.</p>
 */
export function CampoDinamico({ c, valor, desabilitado, onChange, sistema }: Props) {
  const rotulo = `${c.rotulo}${c.obrigatorio ? ' *' : ''}`;
  const id = `din-${c.numero}`;

  if (c.tipo === 'textarea') {
    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-96 flex-1">
        <textarea
          id={id}
          rows={3}
          value={valor}
          disabled={desabilitado}
          onChange={(e) => onChange(e.target.value)}
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100"
        />
      </Campo>
    );
  }

  // Múltipla escolha. O sistema recebe o MESMO nome repetido, um par por opção marcada; no
  // rascunho isso cabe num par nome→valor porque os valores viajam juntos separados por quebra
  // de linha — o contrato de `*ValorMultiplo` no backend, que desdobra na hora do envio.
  if (c.tipo === 'checkbox' && c.opcoes?.length) {
    const marcadas = new Set(valor ? valor.split('\n').filter(Boolean) : []);
    const alternar = (v: string) => {
      if (marcadas.has(v)) marcadas.delete(v);
      else marcadas.add(v);
      // Reordena pela ordem do catálogo, não pela ordem dos cliques.
      onChange(
        c.opcoes!
          .filter((o) => marcadas.has(o.valor))
          .map((o) => o.valor)
          .join('\n'),
      );
    };

    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-72">
        <div id={id} className="flex flex-wrap gap-x-4 gap-y-1.5 pt-1">
          {c.opcoes.map((o) => (
            <label key={o.valor} className="flex items-center gap-1.5 text-sm">
              <input
                type="checkbox"
                value={o.valor}
                disabled={desabilitado}
                checked={marcadas.has(o.valor)}
                onChange={() => alternar(o.valor)}
              />
              {o.rotulo}
            </label>
          ))}
        </div>
      </Campo>
    );
  }

  // Escolha SEM opção nenhuma = catálogo copiado antes da correção de 10/08/2026. Não deixo isso
  // virar caixa de texto em silêncio: o sistema só aceita os valores da lista dele, e o pedido
  // voltaria recusado com o campo aparentemente preenchido na tela.
  if ((c.tipo === 'radio' || c.tipo === 'checkbox') && !c.opcoes?.length) {
    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-72">
        <Input id={id} value={valor} disabled onChange={() => {}} />
        <p className="mt-1 text-xs text-amber-700">
          Sem as opções deste campo. Recopie o catálogo do {sistema} em Configurações para
          liberá-lo.
        </p>
      </Campo>
    );
  }

  if ((c.tipo === 'select' || c.tipo === 'radio') && c.opcoes?.length) {
    if (c.tipo === 'radio') {
      return (
        <Campo label={rotulo} htmlFor={id} className="min-w-72">
          <div id={id} className="flex flex-wrap gap-3 pt-1">
            {c.opcoes.map((o) => (
              <label key={o.valor} className="flex items-center gap-1.5 text-sm">
                <input
                  type="radio"
                  name={c.campo}
                  value={o.valor}
                  disabled={desabilitado}
                  checked={valor === o.valor}
                  onChange={() => onChange(o.valor)}
                />
                {o.rotulo}
              </label>
            ))}
          </div>
        </Campo>
      );
    }

    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-72">
        <Select
          id={id}
          value={valor}
          disabled={desabilitado}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">Selecione…</option>
          {c.opcoes.map((o) => (
            <option key={o.valor} value={o.valor}>
              {o.rotulo}
            </option>
          ))}
        </Select>
      </Campo>
    );
  }

  if (c.tipo === 'date') {
    return (
      <Campo label={rotulo} htmlFor={id} className="w-52">
        <Input
          id={id}
          type="date"
          value={paraIso(valor)}
          disabled={desabilitado}
          onChange={(e) => onChange(paraBr(e.target.value))}
        />
      </Campo>
    );
  }

  return (
    <Campo label={rotulo} htmlFor={id} className="w-56">
      <Input
        id={id}
        value={valor}
        disabled={desabilitado}
        onChange={(e) => onChange(e.target.value)}
      />
    </Campo>
  );
}
