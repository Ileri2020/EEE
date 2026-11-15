targets = ['eight_bit_cpu', 'three_phase_transformer', 'push_pull_converter', 'full_bridge_smps', 'three_phase_controlled_rectifier']
with open('Services/ExampleCircuitService.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()
for i, line in enumerate(lines):
    for t in targets:
        if f'("{t}"' in line:
            print(f'Line {i+1}: {line.rstrip()[:200]}')

