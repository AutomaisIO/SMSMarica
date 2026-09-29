import { useState } from 'react';
import { Select } from '@/shared/ui/Select';
import { TelaSimulada } from '@/features/manual/components/TelaSimulada';
import { IlustracaoVeiculo } from '@/features/veiculos/components/IlustracaoVeiculo';
import { SeletorCorVeiculo } from '@/features/veiculos/components/SeletorCorVeiculo';
import { escolherDesenho } from '@/features/veiculos/lib/desenhoVeiculo';
import { ROTULOS_TIPO_VEICULO, type TipoVeiculo } from '@/features/veiculos/types';

type Exemplo = { rotulo: string; tipo: TipoVeiculo; fabricante: string; modelo: string };

// Os modelos da frota (desenho próprio) e alguns que caem no genérico — para a pessoa ver a
// diferença entre "Desenho do …" e "Desenho genérico · …".
const EXEMPLOS: Exemplo[] = [
  { rotulo: 'Chevrolet Onix', tipo: 'Carro', fabricante: 'Chevrolet', modelo: 'Onix' },
  { rotulo: 'Volkswagen Polo', tipo: 'Carro', fabricante: 'Volkswagen', modelo: 'Polo' },
  { rotulo: 'Chevrolet Spin', tipo: 'Carro', fabricante: 'Chevrolet', modelo: 'Spin' },
  { rotulo: 'Mercedes-Benz Sprinter', tipo: 'Van', fabricante: 'Mercedes-Benz', modelo: 'Sprinter' },
  { rotulo: 'Renault Master adaptada', tipo: 'Van', fabricante: 'Renault', modelo: 'Master adaptada' },
  { rotulo: 'Renault Master (ambulância)', tipo: 'Ambulancia', fabricante: 'Renault', modelo: 'Master' },
  { rotulo: 'Jeep Renegade (genérico)', tipo: 'Carro', fabricante: 'Jeep', modelo: 'Renegade' },
  { rotulo: 'Fiat Strada (genérico)', tipo: 'Carro', fabricante: 'Fiat', modelo: 'Strada' },
  { rotulo: 'Volare W9 (genérico)', tipo: 'MicroOnibus', fabricante: 'Volare', modelo: 'W9' },
];

/**
 * A paleta de cor do cadastro de veículos, de mentira: escolhe o modelo, clica na cor, o desenho
 * repinta. Usa os MESMOS componentes da tela real — se o desenho mudar lá, muda aqui junto.
 */
export function SimulacaoDesenhoVeiculo() {
  const [indice, setIndice] = useState(3);
  const [cor, setCor] = useState('Branca');
  const ex = EXEMPLOS[indice];
  const desenho = escolherDesenho(ex.tipo, ex.modelo, ex.fabricante);

  return (
    <TelaSimulada
      titulo="Simulação · cor e desenho"
      descricao="Troque o modelo e a cor: nada é gravado, é só para ver o desenho mudar."
      aoReiniciar={() => {
        setIndice(3);
        setCor('Branca');
      }}
    >
      <div className="grid grid-cols-1 gap-4 rounded-lg border border-gray-200 bg-white p-4 md:grid-cols-[minmax(0,1fr)_15rem]">
        <div className="space-y-4">
          <div className="flex flex-col gap-1">
            <label htmlFor="sim-modelo" className="label">
              Modelo
            </label>
            <Select id="sim-modelo" value={indice} onChange={(e) => setIndice(Number(e.target.value))}>
              {EXEMPLOS.map((e, i) => (
                <option key={e.rotulo} value={i}>
                  {e.rotulo} — {ROTULOS_TIPO_VEICULO[e.tipo]}
                </option>
              ))}
            </Select>
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="sim-cor" className="label">
              Cor
            </label>
            <SeletorCorVeiculo id="sim-cor" valor={cor} aoMudar={setCor} />
          </div>
        </div>
        <div className="flex flex-col items-center justify-center gap-2 rounded-lg border border-gray-200 bg-gradient-to-b from-gray-50 to-white p-3">
          <IlustracaoVeiculo
            tipo={ex.tipo}
            modelo={ex.modelo}
            fabricante={ex.fabricante}
            cor={cor}
            className="w-full"
          />
          <span className="text-center text-xs text-gray-500">
            {desenho.fiel ? `Desenho do ${desenho.nome}` : `Desenho genérico · ${desenho.nome}`}
          </span>
        </div>
      </div>
    </TelaSimulada>
  );
}
