import psycopg2
conn = psycopg2.connect(host='ep-withered-glade-a1ufh2nv-pooler.ap-southeast-1.aws.neon.tech', database='neondb', user='neondb_owner', password='npg_eJIXBabCp75U', port='5432')
cur = conn.cursor()
cur.execute("SELECT c_email, c_role FROM t_users WHERE c_role = 'field_officer' OR c_role = 'FieldOfficer' LIMIT 5;")
rows = cur.fetchall()
print('Users:', rows)
conn.close()
