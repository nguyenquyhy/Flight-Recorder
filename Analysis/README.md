## Analysis of flight track

The ft.ipynb is a Python Jupyter workbook to analyzer a CSV export from the Flight Tracker.

### Setup

You will need a Python environment set up on your computer. I reccomend creating a virtual environment with all the dependancies to run the workbook code.
```bash
python -m venv venv  # replace python with py on windows
. venv/bin/activate  # or run venv\Scripts\activate  on windows
pip install -r requires.txt
```

### Running the workbook

Visual Studio Code is an excellent environment to work with jupyter workbooks. It requires you have the python extension installed in vscode.
It can also be run in a browser through jupyter by installing into your virtual env
```bash
pip install jupyter
jupyter notebook ftipynb
```