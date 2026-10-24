# Gera um model.onnx com a MESMA estrutura do pipeline real (9 features float,
# Pipeline[FunctionTransformer -> RandomForest], classes ['attack','move'],
# zipmap=False) + um CSV com features e probabilidades do sklearn pra comparar.
import numpy as np, pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import FunctionTransformer
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

FEATS = ['x','y','velocity_x','velocity_y','speed',
         'time_since_last_action','time_since_last_hurt','last_damage_taken',
         'prev_action_was_attack']  # == train_model.py::ALL_FEATURES
rng = np.random.default_rng(0)
n = 3000
X = pd.DataFrame({
 'x': rng.integers(0,200,n), 'y': rng.integers(0,200,n),
 'velocity_x': rng.normal(0,10,n), 'velocity_y': rng.normal(0,10,n),
 'time_since_last_action': rng.uniform(0,5,n),
 'time_since_last_hurt': rng.choice([-1.0]+list(rng.uniform(0,10,50)), n),
 'last_damage_taken': rng.integers(0,30,n),
 'prev_action_was_attack': rng.integers(0,2,n)})
X['speed'] = np.hypot(X.velocity_x, X.velocity_y)
X = X[FEATS]
# sinal artificial so o modelo nao ser constante: ataque mais provavel com acao recente
p = 1/(1+np.exp(-(3*(1-X.time_since_last_action/2.5) + X.prev_action_was_attack - 1.5)))
y = np.where(rng.random(n) < p, 'attack', 'move')
pipe = Pipeline([('prep', FunctionTransformer(validate=False)),
                 ('clf', RandomForestClassifier(n_estimators=40, random_state=0))]).fit(X, y)
print('classes_:', list(pipe.classes_))
onx = convert_sklearn(pipe, initial_types=[('input', FloatTensorType([None,len(FEATS)]))],
                      options={id(pipe.named_steps['clf']): {'zipmap': False}})
import joblib; joblib.dump(pipe,'pipe.pkl')
open('model.onnx','wb').write(onx.SerializeToString())
S = X.sample(60, random_state=1).copy()
probs = pipe.predict_proba(S)
S['p_attack'] = probs[:, list(pipe.classes_).index('attack')]
S.to_csv('rows.csv', index=False)
print(S[['time_since_last_action','prev_action_was_attack','p_attack']].head(3))
