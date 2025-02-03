<template>
  <div>
    <h1>ToDoApp Page</h1>
    <table>
      <thead>
        <tr>
          <th>Id</th>
          <th>Title</th>
          <th>State</th>
          <th>Switcher</th>
          <th>Content</th>
          <th>Actions</th>
        </tr>
        <tr>
          <td><!--<input v-model="newItem.id" placeholder="ID" />--></td>
          <td><input v-model="newItem.title" maxlength="255" placeholder="Title" /></td>
          <td>
            <select v-model="newItem.state">
              <option value="1">Open</option>
              <option value="2">In Progress</option>
              <option value="3">Finished</option>
            </select>
          </td>
          <td></td>
            <!--<td><input v-model="newItem.content" maxlength="2000" placeholder="Content" /></td>
    <td><button @click="addItem">Add</button></td>-->
          <td rowspan="2"><textarea v-model="newItem.content" maxlength="2000" placeholder="Content" rows="4" cols="50"></textarea></td>
          <td rowspan="2"><button class="simple-button" @click="addItem">Add</button></td>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in items" :key="item.id">
          <td>{{ item.id }}</td>
          <td>{{ item.title }}</td>
          <td>{{ getStateText(item.state) }}</td>
          <td>
            <div>
              <button class="simple-button" @click="updateItemState(item.id, 1)">Open</button>
            </div>
            <div>
              <button class="simple-button" @click="updateItemState(item.id, 2)">In Progress</button>
            </div>
            <div>
              <button class="simple-button" @click="updateItemState(item.id, 3)">Finished</button>
            </div>
          </td>
          <td>{{ item.content }}</td>
          <td><button class="simple-button" @click="confirmDeleteItem(item.id)">Delete</button></td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script>
  import { computed, onMounted, ref } from 'vue'
  import { useStore } from 'vuex'

  export default {
    setup() {
      const store = useStore()
      const items = computed(() => store.getters.items)
      const newItem = ref({ title: '', state: '1', content: '' })

      onMounted(() => {
        store.dispatch('fetchItems')
      })

      const addItem = async () => {
        store.dispatch('addItem', newItem.value)
        newItem.value = { title: '', state: '1', content: '' }
        await store.dispatch('fetchItems') // Fetch the updated list of items
        //location.reload()
      }

      const confirmDeleteItem = (id) => {
        if (confirm('Are you sure you want to delete this item?')) {
          deleteItem(id)
        }
      }

      const deleteItem = async (id) => {
        await store.dispatch('deleteItem', id)
        await store.dispatch('fetchItems') // Fetch the updated list of items
        //location.reload()
      }

      const updateItemState = async (id, state) => {
        const item = items.value.find(item => item.id === id)
        if (item) {
          item.state = state
          await store.dispatch('updateItem', item)
          await store.dispatch('fetchItems') // Fetch the updated list of items
        }
      }

      const getStateText = (state) => {
        switch (state) {
          case 1:
            return 'Open'
          case 2:
            return 'In Progress'
          case 3:
            return 'Finished'
          default:
            return 'Unknown'
        }
      }

      return {
        items,
        newItem,
        addItem,
        confirmDeleteItem,
        deleteItem,
        updateItemState,
        getStateText
      }
    }
  }
</script>

<style scoped>
  table {
    width: 100%;
    border-collapse: collapse;
  }

  th, td {
    border: 3px solid #ddd;
    padding: 8px;
  }

  th {
    background-color: #f2f2f2;
    text-align: left;
  }

  .simple-button {
    width: 90px;
    height: 22px;
    margin: 2px 0;
  }
</style>

<style>
  body {
    font-family: Arial, sans-serif;
  }
</style>
